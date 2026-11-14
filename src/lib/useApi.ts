import { useCallback, useEffect, useState } from 'react'
import { getApiErrorMessage } from './apiError'

type ApiState<T> = {
  data: T | null
  loading: boolean
  error: string | null
}

export function useApi<T>(loader: () => Promise<T>, dependencies: unknown[]) {
  const [state, setState] = useState<ApiState<T>>({
    data: null,
    loading: true,
    error: null,
  })
  const [version, setVersion] = useState(0)

  const reload = useCallback(() => {
    setVersion((current) => current + 1)
  }, [])

  useEffect(() => {
    let active = true

    // oxlint-disable-next-line react/set-state-in-effect -- memuat data eksternal dari API
    setState((current) => ({ ...current, loading: true, error: null }))

    loader()
      .then((data) => {
        if (active) {
          setState({ data, loading: false, error: null })
        }
      })
      .catch((error: unknown) => {
        if (active) {
          setState({
            data: null,
            loading: false,
            error: getApiErrorMessage(error),
          })
        }
      })

    return () => {
      active = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...dependencies, version])

  return { ...state, reload }
}
