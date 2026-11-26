import type { components } from '../../../shared/api/schema'

export type QuestionForReview = components['schemas']['QuestionForReview']
export type OwnerQueueItem = components['schemas']['OwnerQueueItem']
export type AiFinding = components['schemas']['AiFinding']
export type Difficulty = components['schemas']['Difficulty']
export type AnswerSpec = components['schemas']['AnswerSpec']

export const difficulties: Difficulty[] = ['Easier', 'OnLevel', 'Harder']
export const difficultyLabel: Record<Difficulty, string> = { Easier: 'Easier', OnLevel: 'On level', Harder: 'Harder' }

export type Choice = { label: string; text: string }

/** The parts of a question's content an owner edits. Anything else in the JSON is kept as it is. */
export type ItemContent = {
  image?: string
  stem: string
  choices: Choice[]
  explanation: string
  figureDescription: string
  showImage: boolean
  rest: Record<string, unknown>
}

const asObject = (value: unknown): Record<string, unknown> =>
  value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {}

const asString = (value: unknown) => (typeof value === 'string' ? value : '')

export function readContent(value: unknown): ItemContent {
  const { image, stem, choices, explanation, figureDescription, showImage, ...rest } = asObject(value)
  return {
    image: typeof image === 'string' ? image : undefined,
    stem: asString(stem),
    choices: Array.isArray(choices)
      ? choices.map((c) => ({ label: asString(asObject(c).label), text: asString(asObject(c).text) }))
      : [],
    explanation: asString(explanation),
    figureDescription: asString(figureDescription),
    showImage: typeof showImage === 'boolean' ? showImage : true,
    rest,
  }
}

export function writeContent(content: ItemContent): Record<string, unknown> {
  const { rest, image, stem, choices, explanation, figureDescription, showImage } = content
  const out: Record<string, unknown> = { ...rest, stem, showImage }
  delete out.textNeedsReview
  if (image) out.image = image
  if (choices.length > 0) out.choices = choices
  if (explanation.trim()) out.explanation = explanation.trim()
  if (figureDescription.trim()) out.figureDescription = figureDescription.trim()
  return out
}

/** "2022 · item 7 · 3.4(K)": where an item came from, from curriculum data. */
export function sourceLine(source: QuestionForReview['source'], skillCodes: string[]): string {
  const parts = source ? [`${source.exam} ${source.year}`, `item ${source.itemNumber}`] : ['No source form']
  return [...parts, ...skillCodes].join(' · ')
}
