import { ButtonHTMLAttributes } from "react";

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "danger";
};

export function Button({ variant = "primary", className = "", ...props }: Props) {
  const variants: Record<string, string> = {
    primary: "bg-slate-900 text-white hover:bg-slate-700",
    secondary: "bg-slate-200 text-slate-900 hover:bg-slate-300",
    danger: "bg-red-600 text-white hover:bg-red-500",
  };

  return (
    <button
      className={`rounded-md px-3 py-2 text-sm font-medium transition ${variants[variant]} ${className}`.trim()}
      {...props}
    />
  );
}
