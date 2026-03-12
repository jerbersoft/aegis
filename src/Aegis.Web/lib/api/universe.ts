import {
  AddSymbolToWatchlistRequest,
  CreateWatchlistRequest,
  ExecutionRemovalBlockersView,
  RenameWatchlistRequest,
  WatchlistContentsView,
  WatchlistDetailView,
  WatchlistSummaryView,
} from "../types/universe";
import { apiRequest } from "./client";

const baseUrl = "/api/universe";

export async function getWatchlists(): Promise<WatchlistSummaryView[]> {
  return apiRequest<WatchlistSummaryView[]>(`${baseUrl}/watchlists`);
}

export async function createWatchlist(request: CreateWatchlistRequest): Promise<WatchlistDetailView> {
  return apiRequest<WatchlistDetailView>(`${baseUrl}/watchlists`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function renameWatchlist(watchlistId: string, request: RenameWatchlistRequest): Promise<WatchlistDetailView> {
  return apiRequest<WatchlistDetailView>(`${baseUrl}/watchlists/${watchlistId}`, {
    method: "PUT",
    body: JSON.stringify(request),
  });
}

export async function deleteWatchlist(watchlistId: string): Promise<void> {
  await apiRequest<void>(`${baseUrl}/watchlists/${watchlistId}`, {
    method: "DELETE",
    parseJson: false,
  });
}

export async function getWatchlistSymbols(watchlistId: string, search?: string): Promise<WatchlistContentsView> {
  const query = search ? `?search=${encodeURIComponent(search)}` : "";
  return apiRequest<WatchlistContentsView>(`${baseUrl}/watchlists/${watchlistId}/symbols${query}`);
}

export async function addSymbolToWatchlist(watchlistId: string, request: AddSymbolToWatchlistRequest) {
  return apiRequest(`${baseUrl}/watchlists/${watchlistId}/symbols`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function removeSymbolFromWatchlist(watchlistId: string, symbolId: string): Promise<void> {
  await apiRequest<void>(`${baseUrl}/watchlists/${watchlistId}/symbols/${symbolId}`, {
    method: "DELETE",
    parseJson: false,
  });
}

export async function getExecutionRemovalBlockers(symbolId: string): Promise<ExecutionRemovalBlockersView> {
  return apiRequest<ExecutionRemovalBlockersView>(`${baseUrl}/execution/symbols/${symbolId}/removal-blockers`);
}
