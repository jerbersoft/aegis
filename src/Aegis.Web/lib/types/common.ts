export type ApiError = {
  code: string;
  message: string;
  details?: Record<string, string[]>;
};

export type SortDirection = "asc" | "desc";
