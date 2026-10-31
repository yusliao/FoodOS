/**
 * Copies text to the clipboard. `navigator.clipboard` only exists in secure contexts (HTTPS or
 * localhost); on a plain-HTTP deployment it is undefined, so fall back to a hidden textarea +
 * `execCommand("copy")`, which browsers still allow inside a click handler. Rejects when both fail.
 */
export async function copyText(text: string): Promise<void> {
  if (window.isSecureContext && navigator.clipboard?.writeText) {
    try {
      await navigator.clipboard.writeText(text);
      return;
    } catch {
      // Permission denied or document not focused: try the legacy path below.
    }
  }

  const textarea = document.createElement("textarea");
  textarea.value = text;
  textarea.setAttribute("readonly", "");
  textarea.style.position = "fixed";
  textarea.style.top = "0";
  textarea.style.left = "0";
  textarea.style.opacity = "0";
  const active = document.activeElement instanceof HTMLElement ? document.activeElement : null;
  // Mount next to the clicked control: a modal's focus trap (Radix Dialog) would pull focus back
  // from a textarea on <body>, leaving nothing selected to copy.
  (active?.parentElement ?? document.body).appendChild(textarea);
  textarea.focus();
  textarea.select();
  let copied = false;
  try {
    copied = document.execCommand("copy");
  } finally {
    textarea.remove();
    active?.focus();
  }
  if (!copied) throw new Error("Clipboard is unavailable.");
}
