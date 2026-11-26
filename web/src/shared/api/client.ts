import createClient from 'openapi-fetch'
import { env } from '../config/env'
import type { paths } from './schema'

/**
 * Typed API client generated from the API's OpenAPI spec (`npm run gen:api`).
 * Features call the API only through this client, so a changed endpoint fails the type check.
 */
export const api = createClient<paths>({ baseUrl: env.apiUrl })
