import { useIsMutating, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  addChatReaction,
  pinChatMessage,
  removeChatReaction,
  unpinChatMessage,
  type ChatMessage,
} from "@/api/chat";
import { useAuth } from "@/auth/use-auth";
import { Button } from "@/components/ui/button";
import { ErrorBand } from "@/components/list";
import { useT } from "@/i18n/locale-provider";
import { ChatPermissions } from "@/lib/permissions";
import { describe } from "@/pages/customers/request-error";

const REACTIONS = ["👍", "❤️", "🎉"] as const;

export function MessageEngagement({ message }: { message: ChatMessage }) {
  const { user } = useAuth();
  const t = useT();
  const cache = useQueryClient();
  const canWrite = user?.tenant === "root"
    && user.permissions.includes(ChatPermissions.View)
    && user.permissions.includes(ChatPermissions.Send)
    && !message.deletedAtUtc;
  const busy = useIsMutating({ mutationKey: ["chat", "write", message.channelId] }) > 0;

  const refresh = async () => {
    await cache.invalidateQueries({ queryKey: ["chat", user?.id] });
  };
  const pin = useMutation({
    mutationKey: ["chat", "write", message.channelId],
    mutationFn: () => message.isPinned ? unpinChatMessage(message.id) : pinChatMessage(message.id),
    onSuccess: refresh,
  });
  const reaction = useMutation({
    mutationKey: ["chat", "write", message.channelId],
    mutationFn: (emoji: string) => message.reactions.some(item => item.userId === user?.id && item.emoji === emoji)
      ? removeChatReaction({ messageId: message.id, emoji })
      : addChatReaction({ messageId: message.id, emoji }),
    onSuccess: refresh,
  });

  if (!canWrite) return null;
  const failure = pin.error ?? reaction.error;
  return <div className="space-y-2">
    <div className="flex flex-wrap items-center gap-2">
      <Button
        size="sm"
        variant="outline"
        disabled={busy}
        onClick={() => { pin.reset(); reaction.reset(); pin.mutate(); }}
      >
        {t(message.isPinned ? "chatDiscovery.unpin" : "chatDiscovery.pin")}
      </Button>
      <span className="text-sm text-[var(--color-muted-foreground)]">{t("chatDiscovery.react")}</span>
      {REACTIONS.map(emoji => {
        const mine = message.reactions.some(item => item.userId === user?.id && item.emoji === emoji);
        const count = message.reactions.filter(item => item.emoji === emoji).length;
        return <Button
          key={emoji}
          size="sm"
          variant={mine ? "signal" : "outline"}
          aria-pressed={mine}
          aria-label={`${t(mine ? "chatDiscovery.removeReaction" : "chatDiscovery.addReaction")} ${emoji}`}
          disabled={busy}
          onClick={() => { pin.reset(); reaction.reset(); reaction.mutate(emoji); }}
        >
          {emoji}{count > 0 ? ` ${count}` : ""}
        </Button>;
      })}
    </div>
    {failure && <ErrorBand message={describe(failure, t("chatDiscovery.actionFailed"))} />}
  </div>;
}
