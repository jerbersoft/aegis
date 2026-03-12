export type SessionView = {
  username: string;
  isAuthenticated: boolean;
  authenticatedAtUtc: string;
};

export type LoginRequest = {
  username: string;
  password: string;
};
