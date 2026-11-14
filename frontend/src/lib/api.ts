import axios from 'axios'
import type { AxiosError, InternalAxiosRequestConfig } from 'axios'
import { useAuthStore } from '../stores/authStore'
import type { AuthSession } from '../stores/authStore'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

export { API_BASE_URL }

export const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
  withCredentials: true,
})

api.interceptors.request.use((config) => {
  const accessToken = useAuthStore.getState().accessToken
  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }
  return config
})

type RetryableConfig = InternalAxiosRequestConfig & { _retry?: boolean }

let refreshPromise: Promise<string | null> | null = null

/**
 * Single-flight refresh: beberapa request 401 bersamaan hanya memicu satu
 * rotasi refresh token. Ini penting karena token lama langsung dicabut.
 */
async function refreshAccessToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = axios
      .post<AuthSession>(`${API_BASE_URL}/auth/refresh`, null, {
        withCredentials: true,
        timeout: 15_000,
      })
      .then((response) => {
        useAuthStore.getState().setSession(response.data)
        return response.data.accessToken
      })
      .catch(() => {
        useAuthStore.getState().clearSession()
        return null
      })
      .finally(() => {
        refreshPromise = null
      })
  }

  return refreshPromise
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetryableConfig | undefined
    const url = config?.url ?? ''
    const isAuthEndpoint = url.includes('/auth/')
    const status = error.response?.status

    if (status === 401 && config && !config._retry && !isAuthEndpoint) {
      config._retry = true
      const accessToken = await refreshAccessToken()

      if (accessToken) {
        config.headers.Authorization = `Bearer ${accessToken}`
        return api.request(config)
      }
    }

    if (status === 401 && !isAuthEndpoint) {
      useAuthStore.getState().clearSession()
    }

    return Promise.reject(error)
  },
)
