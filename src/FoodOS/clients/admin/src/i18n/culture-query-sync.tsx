import { useEffect, useRef } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useLocale } from "@/i18n/locale-provider";

/** Refetch cached API data when the UI culture changes so localized names follow Accept-Language. */
export function CultureQuerySync() {
  const { culture } = useLocale();
  const queryClient = useQueryClient();
  const first = useRef(true);

  useEffect(() => {
    if (first.current) {
      first.current = false;
      return;
    }
    void queryClient.invalidateQueries();
  }, [culture, queryClient]);

  return null;
}
