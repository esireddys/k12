/** "2022 · item 7": where a released question came from, from the import's data. */
export function sourceText(source: { exam: string; year: number | string; itemNumber: number | string } | null | undefined) {
  return source ? `${source.exam} ${source.year} · item ${source.itemNumber}` : null
}
