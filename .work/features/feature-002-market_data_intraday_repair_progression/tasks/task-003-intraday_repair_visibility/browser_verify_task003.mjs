import assert from "node:assert/strict";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire("/Users/herbertsabanal/Projects/.aegis-worktrees/feature-002-market_data_intraday_repair_progression-impl-01/src/Aegis.Web/package.json");
const { chromium } = require("playwright");

const baseUrl = (process.env.AEGIS_WEB_URL ?? "http://127.0.0.1:3001").replace(/\/$/, "");
const taskFolder = "/Users/herbertsabanal/Projects/aegis/.work/features/feature-002-market_data_intraday_repair_progression/tasks/task-003-intraday_repair_visibility";

async function readJson(response, label) {
  const text = await response.text();
  let json;

  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    throw new Error(`${label} returned non-JSON (${response.status()}): ${text}`);
  }

  if (!response.ok()) {
    throw new Error(`${label} failed (${response.status()}): ${JSON.stringify(json)}`);
  }

  return json;
}

async function readJsonAllow404(response, label) {
  const text = await response.text();
  let json;

  try {
    json = text ? JSON.parse(text) : null;
  } catch {
    throw new Error(`${label} returned non-JSON (${response.status()}): ${text}`);
  }

  if (response.status() === 404) {
    return { status: 404, json };
  }

  if (!response.ok()) {
    throw new Error(`${label} failed (${response.status()}): ${JSON.stringify(json)}`);
  }

  return { status: response.status(), json };
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  const page = await context.newPage();
  const request = context.request;

  try {
    await page.goto(`${baseUrl}/login`, { waitUntil: "networkidle", timeout: 120000 });
    await page.getByPlaceholder("Username").fill("task003-tester");
    await page.getByPlaceholder("Password").fill("task003-tester");
    await page.getByRole("button", { name: "Login" }).click();
    await page.waitForURL(`${baseUrl}/home`, { timeout: 120000 });

    const watchlists = await readJson(await request.get(`${baseUrl}/api/universe/watchlists`), "list watchlists");
    const execution = watchlists.find((watchlist) => watchlist.isExecution === true);
    assert.ok(execution, "Expected seeded Execution watchlist.");

    const addSymbolResponse = await request.post(`${baseUrl}/api/universe/watchlists/${execution.watchlistId}/symbols`, {
      data: { symbol: "AMD" },
    });

    if (addSymbolResponse.status() !== 201 && addSymbolResponse.status() !== 409) {
      throw new Error(`add execution symbol failed (${addSymbolResponse.status()}): ${await addSymbolResponse.text()}`);
    }

    const preBootstrapRollup = await readJson(
      await request.get(`${baseUrl}/api/market-data/intraday/readiness`),
      "pre-bootstrap intraday readiness",
    );
    const preBootstrapSymbol = await readJsonAllow404(
      await request.get(`${baseUrl}/api/market-data/intraday/readiness/AMD`),
      "pre-bootstrap AMD intraday readiness",
    );

    await page.goto(`${baseUrl}/home`, { waitUntil: "networkidle", timeout: 120000 });
    await page.getByText("MarketData Bootstrap", { exact: true }).waitFor({ timeout: 120000 });
    await page.getByText("Intraday Readiness", { exact: true }).waitFor({ timeout: 120000 });
    const widget = page.getByText("MarketData Bootstrap", { exact: true }).locator("..");
    const preBootstrapWidgetText = await widget.innerText();
    await page.screenshot({ path: path.join(taskFolder, "browser-home-pre-bootstrap.png"), fullPage: true });

    await Promise.all([
      page.waitForResponse(
        (response) => response.url().endsWith("/api/market-data/bootstrap/run") && response.request().method() === "POST" && response.ok(),
        { timeout: 120000 },
      ),
      page.getByRole("button", { name: "Refresh" }).click(),
    ]);

    await page.reload({ waitUntil: "networkidle", timeout: 120000 });
    await page.getByText("Intraday Readiness", { exact: true }).waitFor({ timeout: 120000 });

    const postBootstrapRollup = await readJson(
      await request.get(`${baseUrl}/api/market-data/intraday/readiness`),
      "post-bootstrap intraday readiness",
    );
    const postBootstrapSymbol = await readJson(
      await request.get(`${baseUrl}/api/market-data/intraday/readiness/AMD`),
      "post-bootstrap AMD intraday readiness",
    );

    const postBootstrapWidgetText = await widget.innerText();
    await page.screenshot({ path: path.join(taskFolder, "browser-home-post-bootstrap.png"), fullPage: true });

    assert.equal(postBootstrapRollup.readinessState, "ready", "Expected post-bootstrap intraday rollup ready state.");
    assert.equal(postBootstrapRollup.activeRepairSymbolCount, 0, "Expected no active repairs in fake-provider happy path.");
    assert.equal(postBootstrapRollup.pendingRecomputeSymbolCount, 0, "Expected no pending recompute in fake-provider happy path.");
    assert.equal(postBootstrapSymbol.readinessState, "ready", "Expected AMD intraday readiness to restore to ready.");
    assert.equal(postBootstrapSymbol.hasActiveRepair, false, "Expected AMD to have no active repair after bootstrap.");
    assert.equal(postBootstrapSymbol.pendingRecompute, false, "Expected AMD to have no pending recompute after bootstrap.");
    assert.match(postBootstrapWidgetText, /MARKETDATA BOOTSTRAP/i, "Expected widget title to render.");
    assert.match(postBootstrapWidgetText, /INTRADAY READINESS/i, "Expected widget to render intraday readiness section.");
    assert.match(postBootstrapWidgetText, /State:\s*ready/i, "Expected widget to display ready intraday state.");
    assert.match(postBootstrapWidgetText, /AMD:\s*ready/i, "Expected widget to display AMD ready detail row.");
    assert.match(postBootstrapWidgetText, /Refresh/i, "Expected widget refresh control to render.");

    console.log(JSON.stringify({
      baseUrl,
      executionWatchlistId: execution.watchlistId,
      addSymbolStatus: addSymbolResponse.status(),
      preBootstrap: {
        rollup: {
          readinessState: preBootstrapRollup.readinessState,
          reasonCode: preBootstrapRollup.reasonCode,
          totalSymbolCount: preBootstrapRollup.totalSymbolCount,
          readySymbolCount: preBootstrapRollup.readySymbolCount,
          notReadySymbolCount: preBootstrapRollup.notReadySymbolCount,
          activeRepairSymbolCount: preBootstrapRollup.activeRepairSymbolCount,
          pendingRecomputeSymbolCount: preBootstrapRollup.pendingRecomputeSymbolCount,
          earliestAffectedBarUtc: preBootstrapRollup.earliestAffectedBarUtc ?? null,
        },
        symbol: {
          status: preBootstrapSymbol.status,
          readinessState: preBootstrapSymbol.json?.readinessState ?? null,
          reasonCode: preBootstrapSymbol.json?.reasonCode ?? null,
          hasActiveRepair: preBootstrapSymbol.json?.hasActiveRepair ?? null,
          pendingRecompute: preBootstrapSymbol.json?.pendingRecompute ?? null,
          activeGapType: preBootstrapSymbol.json?.activeGapType ?? null,
          earliestAffectedBarUtc: preBootstrapSymbol.json?.earliestAffectedBarUtc ?? null,
          error: preBootstrapSymbol.status === 404 ? preBootstrapSymbol.json : null,
        },
        widgetText: preBootstrapWidgetText,
      },
      postBootstrap: {
        rollup: {
          readinessState: postBootstrapRollup.readinessState,
          reasonCode: postBootstrapRollup.reasonCode,
          totalSymbolCount: postBootstrapRollup.totalSymbolCount,
          readySymbolCount: postBootstrapRollup.readySymbolCount,
          notReadySymbolCount: postBootstrapRollup.notReadySymbolCount,
          activeRepairSymbolCount: postBootstrapRollup.activeRepairSymbolCount,
          pendingRecomputeSymbolCount: postBootstrapRollup.pendingRecomputeSymbolCount,
          earliestAffectedBarUtc: postBootstrapRollup.earliestAffectedBarUtc ?? null,
        },
        symbol: {
          readinessState: postBootstrapSymbol.readinessState,
          reasonCode: postBootstrapSymbol.reasonCode,
          hasActiveRepair: postBootstrapSymbol.hasActiveRepair,
          pendingRecompute: postBootstrapSymbol.pendingRecompute,
          activeGapType: postBootstrapSymbol.activeGapType ?? null,
          earliestAffectedBarUtc: postBootstrapSymbol.earliestAffectedBarUtc ?? null,
        },
        widgetText: postBootstrapWidgetText,
      },
      screenshots: [
        path.join(taskFolder, "browser-home-pre-bootstrap.png"),
        path.join(taskFolder, "browser-home-post-bootstrap.png"),
      ],
    }, null, 2));
  } finally {
    await browser.close();
  }
}

await main();
