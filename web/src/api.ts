const baseUrl = import.meta.env.VITE_API_URL ?? 'http://localhost:5143'

export type CheckResult = { isCorrect: boolean; understood: boolean; normalized: string }

export async function getHealth(): Promise<boolean> {
  try {
    const res = await fetch(`${baseUrl}/health`)
    return res.ok
  } catch {
    return false
  }
}
