import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/client'
import { currentUserKey } from '../../../shared/auth/useCurrentUser'
import { learnerStore } from '../../../shared/learner/learner'

/** Which sign-ins this server offers: Google when its client id is set, and the developer sign-in locally. */
export function useSignInOptions() {
  return useQuery({
    queryKey: ['auth', 'options'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/auth/options')
      if (error || !data) throw new Error('Could not reach the server')
      return data
    },
    staleTime: Infinity,
  })
}

function useAfterSignIn() {
  const queryClient = useQueryClient()
  return async () => {
    // A guest on this device becomes the parent's family: start from the kid picker.
    learnerStore.clear()
    await queryClient.invalidateQueries({ queryKey: currentUserKey })
    await queryClient.invalidateQueries({ queryKey: ['family'] })
  }
}

/** Sends the Google ID token to the API, which checks it and starts a session cookie. */
export function useProviderSignIn(provider: string) {
  const after = useAfterSignIn()
  return useMutation({
    mutationFn: async (token: string) => {
      const { data, error } = await api.POST('/api/auth/{provider}', { params: { path: { provider } }, body: { token } })
      if (error || !data) throw new Error(error?.detail ?? 'Sign-in failed. Try again.')
      return data
    },
    onSuccess: after,
  })
}

/** Local development only: signs in by email so the family screens work before Google is set up. */
export function useDeveloperSignIn() {
  const after = useAfterSignIn()
  return useMutation({
    mutationFn: async ({ email, name }: { email: string; name?: string }) => {
      const { data, error } = await api.POST('/api/auth/developer', { body: { email, name: name ?? null } })
      if (error || !data) throw new Error(error?.detail ?? 'Sign-in failed.')
      return data
    },
    onSuccess: after,
  })
}
