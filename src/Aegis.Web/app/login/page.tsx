"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { login } from "@/lib/api/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default function LoginPage() {
  const router = useRouter();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    try {
      await login({ username, password });
      router.push("/home");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to login.");
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-950 px-4">
      <div className="w-full max-w-md rounded-xl border border-slate-800 bg-slate-900 p-6 shadow-2xl shadow-slate-950/40">
        <h1 className="mb-2 text-2xl font-semibold text-slate-100">Aegis Login</h1>
        <p className="mb-6 text-sm text-slate-400">v1 accepts any username/password combination.</p>

        <form className="space-y-4" onSubmit={handleSubmit}>
          <Input placeholder="Username" value={username} onChange={(event) => setUsername(event.target.value)} />
          <Input placeholder="Password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} />

          {error ? <p className="text-sm text-red-400">{error}</p> : null}

          <Button className="w-full" type="submit">
            Login
          </Button>
        </form>
      </div>
    </div>
  );
}
