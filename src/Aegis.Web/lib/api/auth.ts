import { LoginRequest, SessionView } from "../types/auth";
import { apiRequest } from "./client";

const backendBaseUrl = (process.env.NEXT_PUBLIC_BACKEND_URL ?? "http://localhost:5078").replace(/\/$/, "");

export async function login(request: LoginRequest): Promise<SessionView> {
  return apiRequest<SessionView>(`${backendBaseUrl}/api/auth/login`, {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function logout(): Promise<void> {
  await apiRequest<void>(`${backendBaseUrl}/api/auth/logout`, {
    method: "POST",
    parseJson: false,
  });
}

export async function getSession(): Promise<SessionView | null> {
  try {
    return await apiRequest<SessionView>(`${backendBaseUrl}/api/auth/session`);
  } catch {
    return null;
  }
}
