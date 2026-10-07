/**
 * SPEC-010 D2 / AC-04: the matching rule of the storage page's search field.
 *
 * Pure functions, no Angular — the rule is tested on its own (AC-10) and the page only wires
 * it to its signals. Matching is on the item name: every whitespace-separated word of the
 * query must occur somewhere in the name, compared case- and accent-insensitively
 * (`kase` finds `Käse`, `creme` finds `Crème`). `ß` is deliberately not folded (EC-02).
 */

const COMBINING_MARKS = /[̀-ͯ]/g;

/** Lower-cased, accents stripped — the comparable form of a name or a query word. */
export function normalizeForSearch(text: string): string {
  return text.normalize('NFD').replace(COMBINING_MARKS, '').toLowerCase();
}

/** The query's words in comparable form; empty for a blank query (EC-01). */
export function queryWords(query: string): string[] {
  return query
    .split(/\s+/)
    .filter((word) => word.length > 0)
    .map(normalizeForSearch);
}

/** True when every word of the query occurs in the name — or the query is blank. */
export function matchesQuery(name: string, query: string): boolean {
  const words = queryWords(query);
  if (words.length === 0) {
    return true;
  }
  const haystack = normalizeForSearch(name);
  return words.every((word) => haystack.includes(word));
}
