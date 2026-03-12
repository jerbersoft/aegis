import { LoginRequest, SessionView } from "../types/auth";
import { apiRequest } from "./client";

export async function login(request: LoginRequest): Promise<SessionView> {
  return apiRequest<SessionView>("http://localhost:5078/api/auth/login", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export async function logout(): Promise<void> {
  await apiRequest<void>("http://localhost:5078/api/auth/logout", {
    method: "POST",
    parseJson: false,
  });
}

export async function getSession(): Promise<SessionView | null> {
  try {
    return await apiRequest<SessionView>("http://localhost:5078/api/auth/session");
  } catch {
    return null;
  }
}
