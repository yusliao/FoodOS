import { useEffect, useState } from "react";

/** Keep remote lookups from starting a new request for every keystroke. */
export function useDebouncedSearch(search: string) {
  const normalized = search.trim();
  const [term, setTerm] = useState(normalized);
  useEffect(() => {
    if (normalized === term) return;
    const timer = setTimeout(() => setTerm(normalized), 250);
    return () => clearTimeout(timer);
  }, [normalized, term]);
  return [term, normalized !== term] as const;
}
