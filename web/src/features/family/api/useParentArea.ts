import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { api } from '../../../shared/api/client'
import type { components } from '../../../shared/api/schema'
import { localDate } from '../../../shared/time/localDate'

export type ParentGate = components['schemas']['ParentGateResponse']
export type KidDashboard = components['schemas']['KidDashboard']
export type SkillStanding = components['schemas']['SkillStanding']
export type UnlockResult = components['schemas']['UnlockResponse']

const gateKey = ['parent', 'gate'] as const

/** Whether the parent has a PIN and whether the parent areas are open on this device right now. */
export function useParentGate() {
  const queryClient = useQueryClient()
  const gate = useQuery({
    queryKey: gateKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family/pin')
      if (error || !data) throw new Error('Could not check the parent PIN')
      return data
    },
    staleTime: 0,
  })

  // Close the parent areas on screen when the server's window runs out.
  const until = gate.data?.parentAreaOpenUntil
  useEffect(() => {
    if (!until) return
    const ms = new Date(until).getTime() - Date.now()
    const timer = window.setTimeout(() => queryClient.invalidateQueries({ queryKey: gateKey }), Math.max(0, ms) + 500)
    return () => window.clearTimeout(timer)
  }, [until, queryClient])

  return gate
}

/** The parent areas are open when the parent has no PIN yet, or entered it (or signed in) a few minutes ago. */
export const isOpen = (gate: ParentGate | undefined) => !!gate && (!gate.hasPin || !!gate.parentAreaOpenUntil)

export function useUnlock() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (pin: string) => {
      const { data, error } = await api.POST('/api/family/pin/unlock', { body: { pin } })
      if (error || !data) throw new Error('Could not check the PIN')
      return data
    },
    onSuccess: (data) => {
      if (data.result === 'Unlocked') return queryClient.invalidateQueries({ queryKey: gateKey })
    },
  })
}

export function useSetPin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (pin: string) => {
      const { data, error } = await api.PUT('/api/family/pin', { body: { pin } })
      if (error || !data) throw new Error(error?.detail ?? 'Could not save the PIN')
      return data
    },
    onSuccess: (data) => queryClient.setQueryData(gateKey, data),
  })
}

/** Closes the parent areas on this device, for handing the tablet back to a kid. */
export function useLock() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/family/pin/lock')
    },
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: ['parent', 'dashboard'] })
      return queryClient.invalidateQueries({ queryKey: gateKey })
    },
  })
}

/** One kid's dashboard. Parents only: the API refuses it while the parent areas are closed. */
export function useKidDashboard(kidId: string | null) {
  return useQuery({
    queryKey: ['parent', 'dashboard', kidId],
    enabled: !!kidId,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/family/kids/{kidId}/dashboard', {
        params: { path: { kidId: kidId! }, query: { localDate: localDate() } },
      })
      if (error || !data) throw new Error(error?.detail ?? 'Could not load the dashboard')
      return data
    },
  })
}
