import type { ReactNode } from "react";
import { Link } from "react-router-dom";

/** Notification payloads are data, not trusted navigation instructions. */
export function NotificationLink({ href, children, fallback = null, className, onClick }: {
  href?: string | null;
  children: ReactNode;
  fallback?: ReactNode;
  className?: string;
  onClick?: () => void;
}) {
  if (!href || href !== href.trim() || /[\\\s]/u.test(href)) return <>{fallback}</>;
  if (href.startsWith("/") && !href.startsWith("//")) {
    return <Link to={href} className={className} onClick={onClick}>{children}</Link>;
  }
  try {
    const url = new URL(href);
    if ((url.protocol === "https:" || url.protocol === "http:") && !url.username && !url.password) {
      return <a href={url.href} target="_blank" rel="noopener noreferrer" className={className} onClick={onClick}>{children}</a>;
    }
  } catch {
    // Malformed and unsupported links leave the notification itself readable.
  }
  return <>{fallback}</>;
}
