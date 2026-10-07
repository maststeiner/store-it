import { matchesQuery, normalizeForSearch, queryWords } from './item-search';

// SPEC-010 AC-04 / AC-10 — the matching rule, independent of the page.
describe('item search rule', () => {
  it('matches a substring of the name regardless of case', () => {
    expect(matchesQuery('Bio Vollmilch', 'milch')).toBe(true);
    expect(matchesQuery('Bio Vollmilch', 'MILCH')).toBe(true);
    expect(matchesQuery('Bio Vollmilch', 'voll')).toBe(true);
    expect(matchesQuery('Bio Vollmilch', 'butter')).toBe(false);
  });

  it('ignores accents in both directions (EC-02)', () => {
    expect(matchesQuery('Käse', 'kase')).toBe(true);
    expect(matchesQuery('Kase', 'käse')).toBe(true);
    expect(matchesQuery('Crème fraîche', 'creme fraiche')).toBe(true);
    expect(matchesQuery('Crème fraîche', 'CRÈME')).toBe(true);
  });

  it('does not fold ß to ss (EC-02)', () => {
    expect(matchesQuery('Weißbrot', 'wei')).toBe(true);
    expect(matchesQuery('Weißbrot', 'weiss')).toBe(false);
  });

  it('requires every word of the query to occur (AND)', () => {
    expect(matchesQuery('Bio Vollmilch 3.5%', 'bio milch')).toBe(true);
    expect(matchesQuery('Bio Vollmilch 3.5%', 'milch bio')).toBe(true);
    expect(matchesQuery('Vollmilch', 'bio milch')).toBe(false);
  });

  it('treats a blank or whitespace-only query as no filter (EC-01)', () => {
    expect(matchesQuery('anything', '')).toBe(true);
    expect(matchesQuery('anything', '   ')).toBe(true);
    expect(queryWords('   ')).toEqual([]);
    expect(queryWords('  bio   Milch ')).toEqual(['bio', 'milch']);
  });

  it('normalises to a lower-cased, accent-free form', () => {
    expect(normalizeForSearch('Crème Brûlée')).toBe('creme brulee');
    expect(normalizeForSearch('ÄÖÜ')).toBe('aou');
  });
});
