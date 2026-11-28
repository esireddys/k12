import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'
import type { components } from '../../../shared/api/schema'
import { currentUserKey } from '../../../shared/auth/useCurrentUser'
import { learnerStore } from '../../../shared/learner/learner'

export type Kid = components['schemas']['KidView']
export type KidBody = components['schemas']['KidBody']

const familyKey = ['family'] as const

/** The signed-in parent's account and kids. */
export function useFamily(enabled = true) {
  return useQuery({
    queryKey: familyKey,
    enabled,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family')
      if (error || !data) throw new Error('Could not load your family')
      return data
    },
  })
}

function useFamilyMutation<TArgs>(run: (args: TArgs) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: familyKey }),
  })
}

const fail = (error: { detail?: string | null } | undefined, fallback: string) => {
  throw new Error(error?.detail ?? fallback)
}

export function useAddKid() {
  return useFamilyMutation(async (body: KidBody) => {
    const { data, error } = await api.POST('/api/family/kids', { body })
    return error || !data ? fail(error, 'Could not add the kid') : data
  })
}

export function useUpdateKid() {
  return useFamilyMutation(async ({ id, ...body }: KidBody & { id: string }) => {
    const { data, error } = await api.PUT('/api/family/kids/{kidId}', { params: { path: { kidId: id } }, body })
    return error || !data ? fail(error, 'Could not save the changes') : data
  })
}

export function useRemoveKid() {
  return useFamilyMutation(async (id: string) => {
    const { data, error } = await api.DELETE('/api/family/kids/{kidId}', { params: { path: { kidId: id } } })
    const current = learnerStore.get()
    if (current?.kind === 'kid' && current.kidId === id) learnerStore.clear()
    return error || !data ? fail(error, 'Could not remove the kid') : data
  })
}

/** Ends the session cookie and forgets who was practicing on this device. */
export function useSignOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/auth/sign-out')
    },
    onSuccess: async () => {
      learnerStore.clear()
      queryClient.removeQueries({ queryKey: familyKey })
      await queryClient.invalidateQueries({ queryKey: currentUserKey })
    },
  })
}
