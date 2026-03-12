import { LoginRequest, SessionView } from "../types/auth";
import { apiRequest } from "./client";

export async function login(request: LoginRequest): Promise<SessionView> {
  return apiRequest<SessionView>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function logout(): Promise<void> {
  await apiRequest<void>("/api/auth/logout", {
    method: "POST",
    parseJson: false,
  });
}

export async function getSession(): Promise<SessionView | null> {
  try {
    return await apiRequest<SessionView>("/api/auth/session");
  } catch {
    return null;
  }
}
