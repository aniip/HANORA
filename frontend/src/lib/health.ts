import { api } from './api'

export type HealthResponse = {
  status: string
  service: string
  version: string
  environment: string
  timestamp: string
}

export type ReadinessResponse = {
  status: 'ready' | 'not_ready'
  database: 'up' | 'down'
  storage: 'writable' | 'unavailable'
  timestamp: string
}

export type ReadinessResult = {
  httpStatus: number
  data: ReadinessResponse
}

export async function getHealth(): Promise<HealthResponse> {
  const response = await api.get<HealthResponse>('/health')
  return response.data
}

export async function getReadiness(): Promise<ReadinessResult> {
  const response = await api.get<ReadinessResponse>('/health/ready', {
    validateStatus: () => true,
  })

  return {
    httpStatus: response.status,
    data: response.data,
  }
}
