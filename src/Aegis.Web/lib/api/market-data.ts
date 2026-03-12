import { apiRequest } from "./client";
import { DailyBarsView, MarketDataBootstrapStatusView } from "../types/market-data";

const baseUrl = "/api/market-data";

export async function getMarketDataBootstrapStatus(): Promise<MarketDataBootstrapStatusView> {
  return apiRequest<MarketDataBootstrapStatusView>(`${baseUrl}/bootstrap/status`);
}

export async function runMarketDataBootstrap(): Promise<MarketDataBootstrapStatusView> {
  return apiRequest<MarketDataBootstrapStatusView>(`${baseUrl}/bootstrap/run`, {
    method: "POST",
  });
}

export async function getDailyBars(symbol: string): Promise<DailyBarsView> {
  return apiRequest<DailyBarsView>(`${baseUrl}/daily-bars/${encodeURIComponent(symbol)}`);
}
