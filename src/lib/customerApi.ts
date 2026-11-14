import axios from 'axios'
import { API_BASE_URL } from './api'

/**
 * Client customer tidak membawa access token staf.
 * Otorisasi customer memakai tabel session token secara eksplisit.
 */
export const customerApi = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15_000,
})
