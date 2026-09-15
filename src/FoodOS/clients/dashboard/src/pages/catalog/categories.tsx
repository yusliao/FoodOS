import {
  useEffect,
  useMemo,
  useState,
  type FormEvent,
} from "react";
import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
} from "@tanstack/react-query";
import {
  ChevronRight,
  ChevronsRight,
  GitBranch,
  Layers,
  Pencil,
  Plus,
  Search,
  Trash2,
} from "lucide-react";
import { toast } from "sonner";
import {
  createCategory,
  deleteCategory,
  getCategoryTree,
  searchCategories,
  updateCategory,
  type CategoryDto,
  type CategoryTreeNodeDto,
  type CreateCategoryInput,
  type UpdateCategoryInput,
} from "@/api/catalog";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  Combobox,
  EntityEmpty,
  EntityInitialsAvatar,
  EntityListCard,
  EntityListHeader,
  EntityListLoading,
  EntityListRow,
  EntityMobileCard,
  EntityPageHeader,
  EntityPager,
  EntitySearch,
  Field,
} from "@/components/list";
import { cn } from "@/lib/cn";
import { useT } from "@/i18n/locale-provider";
import {
  describe,
  formatDate,
  formatRelative,
  slugify,
} from "@/lib/list-helpers";

const PAGE_SIZE = 50;

type FlatNode = {
  id: string;
  name: string;
  slug: string;
  depth: number;
  ancestorIds: string[];
};
function flattenTree(
  nodes: CategoryTreeNodeDto[],
  depth = 0,
  ancestors: string[] = [],
): FlatNode[] {
  const out: FlatNode[] = [];
  for (const n of nodes) {
    out.push({ id: n.id, name: n.name, slug: n.slug, depth, ancestorIds: ancestors });
    if (n.children?.length)
      out.push(...flattenTree(n.children, depth + 1, [...ancestors, n.id]));
  }
  return out;
}

type EditorState =
  | { mode: "closed" }
  | { mode: "create" }
  | { mode: "edit"; category: CategoryDto }
  | { mode: "delete"; category: CategoryDto };

// ───────────────────────────────────────────────────────────────────────
//  Page
// ───────────────────────────────────────────────────────────────────────

export function CategoriesPage() {
  const t = useT();
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [pageNumber, setPageNumber] = useState(1);
  const [editor, setEditor] = useState<EditorState>({ mode: "closed" });

  useEffect(() => {
    const t = setTimeout(() => {
      setDebouncedSearch(search.trim());
      setPageNumber(1);
    }, 250);
    return () => clearTimeout(t);
  }, [search]);

  const query = useQuery({
    queryKey: [
      "catalog",
      "categories",
      { search: debouncedSearch, pageNumber, pageSize: PAGE_SIZE },
    ],
    queryFn: () =>
      searchCategories({
        search: debouncedSearch || undefined,
        pageNumber,
        pageSize: PAGE_SIZE,
        sortBy: "name",
        sortDir: "asc",
      }),
    placeholderData: keepPreviousData,
  });

  const treeQuery = useQuery({
    queryKey: ["catalog", "categories", "tree"],
    queryFn: getCategoryTree,
    staleTime: 30_000,
  });

  const data = query.data;
  const items = data?.items ?? [];

  const nameById = useMemo(() => {
    const map = new Map<string, string>();
    const walk = (nodes: CategoryTreeNodeDto[]) => {
      for (const n of nodes) {
        map.set(n.id, n.name);
        if (n.children?.length) walk(n.children);
      }
    };
    if (treeQuery.data) walk(treeQuery.data);
    return map;
  }, [treeQuery.data]);

  const searchActive = debouncedSearch.length > 0;

  return (
    <div className="space-y-4 sm:space-y-6">
      <EntityPageHeader
        icon={Layers}
        title={t("catalog.categories.title")}
        total={data?.totalCount ?? null}
        unit={t("catalog.categories.unit")}
        description={t("catalog.categories.pageDesc")}
      >
        <Button
          onClick={() => setEditor({ mode: "create" })}
          className="h-9 flex-1 gap-1.5 rounded-lg px-4 text-[13px] font-semibold sm:flex-none"
        >
          <Plus className="size-4" />
          {t("catalog.categories.newCategory")}
        </Button>
      </EntityPageHeader>

      <EntitySearch
        value={search}
        onChange={setSearch}
        placeholder={t("catalog.categories.searchPlaceholder")}
      />

      {query.isLoading && items.length === 0 ? (
        <EntityListLoading desktopColumns="grid-cols-[1fr_180px_140px_24px]" />
      ) : items.length === 0 ? (
        <EntityEmpty
          icon={searchActive ? Search : Layers}
          title={searchActive ? t("catalog.categories.emptySearchTitle") : t("catalog.categories.emptyTitle")}
          body={
            searchActive
              ? debouncedSearch
                ? t("catalog.categories.emptySearchBody").replace("{q}", debouncedSearch)
                : t("catalog.categories.emptyFilterBody")
              : t("catalog.categories.emptyBody")
          }
          action={
            searchActive ? (
              <Button
                variant="outline"
                onClick={() => setSearch("")}
                className="h-9 rounded-lg px-4 text-[13px]"
              >
                {t("identity.clearSearch")}
              </Button>
            ) : (
              <Button
                onClick={() => setEditor({ mode: "create" })}
                className="h-9 rounded-lg px-4 text-[13px]"
              >
                <Plus className="mr-1.5 size-4" />
                {t("catalog.categories.addCategory")}
              </Button>
            )
          }
        />
      ) : (
        <div>
          <div className="mb-3 flex items-center justify-between">
            <p className="text-[12px] font-medium text-[var(--color-muted-foreground)]">
              {(data?.totalCount ?? 0) === 1
                ? t("catalog.categories.foundOne").replace("{n}", String(data?.totalCount ?? 0))
                : t("catalog.categories.found").replace("{n}", String(data?.totalCount ?? 0))}
            </p>
          </div>

          {/* Mobile: card list */}
          <div className="space-y-2 md:hidden">
            {items.map((category) => (
              <MobileCard
                key={category.id}
                category={category}
                parentName={
                  category.parentCategoryId
                    ? nameById.get(category.parentCategoryId)
                    : undefined
                }
                onEdit={() => setEditor({ mode: "edit", category })}
              />
            ))}
          </div>

          {/* Desktop: list card */}
          <EntityListCard className="hidden md:block">
            <EntityListHeader className="grid-cols-[1fr_180px_140px_24px]">
              <span>{t("catalog.categories.colCategory")}</span>
              <span>{t("catalog.slug")}</span>
              <span>{t("catalog.created")}</span>
              <span />
            </EntityListHeader>

            {items.map((category, i) => (
              <DesktopRow
                key={category.id}
                category={category}
                parentName={
                  category.parentCategoryId
                    ? nameById.get(category.parentCategoryId)
                    : undefined
                }
                isLast={i === items.length - 1}
                onEdit={() => setEditor({ mode: "edit", category })}
                onDelete={() => setEditor({ mode: "delete", category })}
              />
            ))}
          </EntityListCard>

          <EntityPager
            page={data?.pageNumber ?? 1}
            totalPages={data?.totalPages ?? 1}
            hasPrev={!!data?.hasPrevious}
            hasNext={!!data?.hasNext}
            onPrev={() => setPageNumber((p) => Math.max(1, p - 1))}
            onNext={() => setPageNumber((p) => p + 1)}
          />
        </div>
      )}

      {query.isError && (
        <div
          role="alert"
          className="rounded-lg border border-[oklch(from_var(--color-destructive)_l_c_h_/_0.30)] bg-[oklch(from_var(--color-destructive)_l_c_h_/_0.06)] px-3 py-2 text-sm text-[var(--color-destructive)]"
        >
          {describe(query.error)}
        </div>
      )}

      <CategoryEditorDialog
        state={editor}
        onClose={() => setEditor({ mode: "closed" })}
        tree={treeQuery.data ?? []}
      />
      <DeleteCategoryDialog
        state={editor}
        onClose={() => setEditor({ mode: "closed" })}
      />
    </div>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Mobile card
// ───────────────────────────────────────────────────────────────────────

function MobileCard({
  category,
  parentName,
  onEdit,
}: {
  category: CategoryDto;
  parentName: string | undefined;
  onEdit: () => void;
}) {
  const t = useT();
  return (
    <EntityMobileCard
      href="#"
      onClick={(e) => {
        e.preventDefault();
        onEdit();
      }}
      aria-label={t("catalog.categories.editAria").replace("{name}", category.name)}
    >
      <div className="flex items-center justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <EntityInitialsAvatar name={category.name} size={40} />
          <div className="min-w-0">
            <p className="truncate text-[14px] font-medium text-[var(--color-foreground)]">
              {category.name}
            </p>
            <code className="mt-0.5 block truncate font-mono text-[11px] text-[var(--color-muted-foreground)]">
              {category.slug}
            </code>
          </div>
        </div>
        <ChevronRight className="size-4 text-[var(--color-border)]" />
      </div>
      <div className="mt-2 ml-[52px] flex flex-wrap items-center gap-1.5 text-[11px] text-[var(--color-muted-foreground)]">
        {category.parentCategoryId ? (
          <>
            <ChevronsRight className="size-3 opacity-60" />
            <span>{t("catalog.categories.under").replace("{name}", parentName ?? t("catalog.categories.parentFallback"))}</span>
          </>
        ) : (
          <>
            <GitBranch className="size-3 opacity-60" />
            <span>{t("catalog.categories.root")}</span>
          </>
        )}
        {category.description && (
          <>
            <span className="opacity-40">·</span>
            <span className="truncate" title={category.description}>
              {category.description}
            </span>
          </>
        )}
      </div>
    </EntityMobileCard>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Desktop row
// ───────────────────────────────────────────────────────────────────────

function DesktopRow({
  category,
  parentName,
  isLast,
  onEdit,
  onDelete,
}: {
  category: CategoryDto;
  parentName: string | undefined;
  isLast: boolean;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const t = useT();
  return (
    <EntityListRow
      className="grid-cols-[1fr_180px_140px_24px]"
      isLast={isLast}
    >
      {/* Name + avatar */}
      <div className="flex min-w-0 items-center gap-3">
        <EntityInitialsAvatar name={category.name} size={36} />
        <div className="min-w-0">
          <div className="truncate text-[14px] font-medium text-[var(--color-foreground)] transition-colors group-hover:text-[var(--color-primary)]">
            {category.name}
          </div>
          <div className="mt-0.5 flex items-center gap-1 truncate text-[12px] text-[var(--color-muted-foreground)]">
            {category.parentCategoryId ? (
              <>
                <ChevronsRight className="size-3 shrink-0 opacity-60" />
                <span className="truncate">
                  {t("catalog.categories.under").replace("{name}", parentName ?? t("catalog.categories.parentFallback"))}
                </span>
              </>
            ) : (
              <>
                <GitBranch className="size-3 shrink-0 opacity-60" />
                <span>{t("catalog.categories.root")}</span>
              </>
            )}
            {category.description && (
              <>
                <span className="opacity-40">·</span>
                <span className="truncate" title={category.description}>
                  {category.description}
                </span>
              </>
            )}
          </div>
        </div>
      </div>

      {/* Slug */}
      <code
        title={category.slug}
        className="truncate font-mono text-[12px] text-[var(--color-muted-foreground)]"
      >
        {category.slug}
      </code>

      {/* Created */}
      <div className="min-w-0 text-[12px] text-[var(--color-muted-foreground)] tabular-nums">
        <div className="truncate">{formatDate(category.createdAtUtc)}</div>
        <div className="truncate text-[11px] opacity-70">
          {formatRelative(category.createdAtUtc)}
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-end gap-1">
        <button
          type="button"
          aria-label={t("catalog.editNamed").replace("{name}", category.name)}
          onClick={onEdit}
          className="grid size-7 cursor-pointer place-items-center rounded-md text-[var(--color-muted-foreground)] opacity-0 transition-all hover:bg-[var(--color-muted)] hover:text-[var(--color-foreground)] group-hover:opacity-100"
        >
          <Pencil className="size-3.5" />
        </button>
        <button
          type="button"
          aria-label={t("catalog.deleteNamed").replace("{name}", category.name)}
          onClick={onDelete}
          className="grid size-7 cursor-pointer place-items-center rounded-md text-[var(--color-muted-foreground)] opacity-0 transition-all hover:bg-[var(--color-muted)] hover:text-[var(--color-destructive)] group-hover:opacity-100"
        >
          <Trash2 className="size-3.5" />
        </button>
        <ChevronRight className="size-4 text-[var(--color-border)] transition-colors group-hover:text-[var(--color-muted-foreground)]" />
      </div>
    </EntityListRow>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Editor dialog
// ───────────────────────────────────────────────────────────────────────

function CategoryEditorDialog({
  state,
  onClose,
  tree,
}: {
  state: EditorState;
  onClose: () => void;
  tree: CategoryTreeNodeDto[];
}) {
  const t = useT();
  const isOpen = state.mode === "create" || state.mode === "edit";
  const category = state.mode === "edit" ? state.category : undefined;
  const queryClient = useQueryClient();

  const initial = useMemo(
    () => ({
      name: category?.name ?? "",
      description: category?.description ?? "",
      parentCategoryId: category?.parentCategoryId ?? "",
    }),
    [category?.name, category?.description, category?.parentCategoryId],
  );

  const [name, setName] = useState(initial.name);
  const [description, setDescription] = useState(initial.description);
  const [parentCategoryId, setParentCategoryId] = useState(initial.parentCategoryId);

  useEffect(() => {
    if (isOpen) {
      setName(initial.name);
      setDescription(initial.description);
      setParentCategoryId(initial.parentCategoryId);
    }
  }, [
    isOpen,
    initial.name,
    initial.description,
    initial.parentCategoryId,
  ]);

  const slugPreview = useMemo(() => slugify(name) || "—", [name]);

  const parentOptions = useMemo<FlatNode[]>(() => {
    const flat = flattenTree(tree);
    if (!category) return flat;
    return flat.filter(
      (n) => n.id !== category.id && !n.ancestorIds.includes(category.id),
    );
  }, [tree, category]);

  const createMutation = useMutation({
    mutationFn: (input: CreateCategoryInput) => createCategory(input),
    onSuccess: () => {
      toast.success(t("catalog.categories.created"));
      queryClient.invalidateQueries({ queryKey: ["catalog", "categories"] });
      onClose();
    },
    onError: (err) => toast.error(t("identity.createFailed"), { description: describe(err) }),
  });

  const updateMutation = useMutation({
    mutationFn: (input: UpdateCategoryInput) => updateCategory(input),
    onSuccess: () => {
      toast.success(t("catalog.categories.updated"));
      queryClient.invalidateQueries({ queryKey: ["catalog", "categories"] });
      onClose();
    },
    onError: (err) => toast.error(t("identity.updateFailed"), { description: describe(err) }),
  });

  const isPending = createMutation.isPending || updateMutation.isPending;
  const trimmedName = name.trim();

  const onSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!trimmedName) return;
    const payload = {
      name: trimmedName,
      description: description.trim() || null,
      parentCategoryId: parentCategoryId || null,
    };
    if (state.mode === "edit" && category) {
      updateMutation.mutate({ categoryId: category.id, ...payload });
    } else {
      createMutation.mutate(payload);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(o) => (!o ? onClose() : undefined)}>
      <DialogContent className="!max-w-lg">
        <form onSubmit={onSubmit}>
          <DialogHeader>
            <DialogTitle>
              {category ? t("catalog.categories.editTitle") : t("catalog.categories.addTitle")}
            </DialogTitle>
            <DialogDescription>
              {category
                ? t("catalog.categories.editDesc").replace("{name}", category.name)
                : t("catalog.categories.addDesc")}
            </DialogDescription>
          </DialogHeader>

          <DialogBody className="space-y-5">
            <Field id="category-name" label={t("catalog.name")} required>
              <Input
                id="category-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Outdoor"
                autoFocus
                required
                maxLength={128}
              />
            </Field>

            <Field
              id="category-slug"
              label={t("catalog.slug")}
              hint={t("catalog.slugHint")}
            >
              <div className="flex h-9 items-center gap-2 rounded-md border border-[var(--color-border)] bg-[var(--color-muted)] px-3">
                <code className="truncate font-mono text-[12.5px] tracking-tight text-[var(--color-foreground)]">
                  {slugPreview}
                </code>
              </div>
            </Field>

            <Field
              id="category-parent"
              label={t("catalog.categories.parent")}
              hint={t("catalog.categories.parentHint")}
            >
              <Combobox
                id="category-parent"
                label={t("catalog.categories.parentLabel")}
                placeholder={t("catalog.categories.noParent")}
                value={parentCategoryId || null}
                onChange={(v) => setParentCategoryId(v ?? "")}
                options={parentOptions.map((opt) => ({
                  value: opt.id,
                  label: opt.name,
                  prefix:
                    opt.depth > 0 ? (
                      <span
                        aria-hidden
                        className="font-mono text-[11px] tracking-tight text-[var(--color-muted-foreground)]"
                        style={{ paddingLeft: `${(opt.depth - 1) * 12}px` }}
                      >
                        ↳
                      </span>
                    ) : (
                      <span
                        aria-hidden
                        className="text-[10.5px] font-semibold uppercase tracking-wider text-[oklch(from_var(--color-muted-foreground)_l_c_h_/_0.6)]"
                      >
                        {t("catalog.categories.root")}
                      </span>
                    ),
                }))}
                searchable
                clearable
                emptyOptionLabel={t("catalog.categories.noParent")}
              />
            </Field>

            <Field
              id="category-description"
              label={t("catalog.description")}
              hint={t("catalog.categories.catDescHint")}
            >
              <textarea
                id="category-description"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                rows={3}
                maxLength={1024}
                className={cn(
                  "flex w-full rounded-lg border border-[var(--color-input)] bg-transparent px-3 py-2 text-sm shadow-xs",
                  "placeholder:text-[oklch(from_var(--color-muted-foreground)_l_c_h_/_0.6)]",
                  "focus-visible:border-[var(--color-ring)] focus-visible:outline-none focus-visible:ring-[3px] focus-visible:ring-[oklch(from_var(--color-ring)_l_c_h_/_0.5)]",
                )}
                placeholder="Gear for the great outdoors."
              />
            </Field>
          </DialogBody>

          <DialogFooter>
            <DialogClose asChild>
              <Button type="button" variant="outline" disabled={isPending}>
                {t("chrome.cancel")}
              </Button>
            </DialogClose>
            <Button type="submit" disabled={isPending || !trimmedName}>
              {isPending ? t("identity.saving") : category ? t("identity.saveChanges") : t("catalog.categories.addCategory")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

// ───────────────────────────────────────────────────────────────────────
//  Delete confirmation
// ───────────────────────────────────────────────────────────────────────

function DeleteCategoryDialog({
  state,
  onClose,
}: {
  state: EditorState;
  onClose: () => void;
}) {
  const t = useT();
  const isOpen = state.mode === "delete";
  const category = state.mode === "delete" ? state.category : undefined;
  const queryClient = useQueryClient();

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteCategory(id),
    onSuccess: () => {
      toast.success(t("catalog.categories.deleted"));
      queryClient.invalidateQueries({ queryKey: ["catalog", "categories"] });
      queryClient.invalidateQueries({ queryKey: ["trash", "categories"] });
      onClose();
    },
    onError: (err) => toast.error(t("identity.deleteFailed"), { description: describe(err) }),
  });

  return (
    <Dialog open={isOpen} onOpenChange={(o) => (!o ? onClose() : undefined)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle className="text-[var(--color-destructive)]">
            {t("catalog.categories.deleteTitle")}
          </DialogTitle>
          <DialogDescription>
            {t("catalog.categories.deleteBody")
              .replace("{name}", category?.name ?? "")
              .replace(
                "{created}",
                category
                  ? t("catalog.createdAgo").replace("{date}", formatDate(category.createdAtUtc))
                  : "",
              )}
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <DialogClose asChild>
            <Button type="button" variant="outline" disabled={deleteMutation.isPending}>
              {t("chrome.cancel")}
            </Button>
          </DialogClose>
          <Button
            variant="destructive"
            onClick={() => category && deleteMutation.mutate(category.id)}
            disabled={deleteMutation.isPending || !category}
          >
            {deleteMutation.isPending ? t("identity.deleting") : t("catalog.categories.deleteCategory")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
