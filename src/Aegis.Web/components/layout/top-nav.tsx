"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { logout } from "@/lib/api/auth";
import { SessionView } from "@/lib/types/auth";

type Props = {
  session: SessionView;
};

export function TopNav({ session }: Props) {
  const router = useRouter();

  async function handleLogout() {
    await logout();
    router.push("/login");
  }

  return (
    <header className="border-b border-slate-800 bg-slate-950/95 backdrop-blur">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
        <div className="flex items-center gap-8">
          <div className="text-lg font-bold text-slate-100">Aegis</div>
          <nav className="flex items-center gap-4 text-sm text-slate-300">
            <Link className="hover:text-white" href="/home">Home</Link>
            <Link className="hover:text-white" href="/watchlists">Watchlists</Link>
          </nav>
        </div>

        <div className="flex items-center gap-4 text-sm text-slate-300">
          <span>{session.username}</span>
          <Link className="hover:text-white" href="/preferences">Preferences</Link>
          <button className="hover:text-white" type="button" onClick={handleLogout}>
            Logout
          </button>
        </div>
      </div>
    </header>
  );
}
