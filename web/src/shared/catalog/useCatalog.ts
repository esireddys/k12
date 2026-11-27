import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type CatalogCourse = components['schemas']['CatalogCourse']
export type CatalogCategory = components['schemas']['CatalogCategory']
export type CatalogForm = components['schemas']['CatalogForm']

/** Every course students can pick, with question counts by topic and by released test. */
export function useCatalog() {
  return useQuery({
    queryKey: ['practice', 'catalog'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/practice/catalog')
      if (error || !data) throw new Error('Could not load the courses')
      return data.courses
    },
    staleTime: 5 * 60_000,
  })
}

/** The jurisdiction's name from curriculum data, or its code until an import names it. */
export const jurisdictionLabel = (c: Pick<CatalogCourse, 'jurisdiction' | 'jurisdictionName'>) =>
  c.jurisdictionName || c.jurisdiction

const subjectLabel = (subject: string) => subject.charAt(0).toUpperCase() + subject.slice(1)

/**
 * "Grade 3 Math" for a whole grade; just "Algebra I" for a course, whose name already says the subject.
 * Both names come from curriculum data.
 */
export const courseLabel = (c: Pick<CatalogCourse, 'name' | 'subject' | 'kind'>) =>
  c.kind === 'Course' ? c.name : `${c.name} ${subjectLabel(c.subject)}`

const gradeName = (grade: number) => (grade === 0 ? 'kindergarten' : `grade ${grade}`)

/** "Usually grade 9" for a course such as Algebra I, so parents know who it is for. Empty for a whole grade. */
export function courseLevelHint(c: Pick<CatalogCourse, 'kind' | 'typicalGrade'>): string {
  if (c.kind !== 'Course' || c.typicalGrade === null || c.typicalGrade === undefined) return ''
  return `Usually ${gradeName(Number(c.typicalGrade))}`
}

/** The exam names this course's released tests come from, for example one name, or two joined with "and". From data. */
export function examNames(c: CatalogCourse): string {
  const names = [...new Set(c.forms.map((f) => f.exam))]
  return names.length === 0 ? '' : names.length === 1 ? names[0] : `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`
}

export const hasQuestions = (c: CatalogCourse) => Number(c.questionCount) > 0
