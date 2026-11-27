/** Today's date on this device as yyyy-MM-dd, so streaks follow the kid's own days rather than the server's. */
export function localDate(now = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}
