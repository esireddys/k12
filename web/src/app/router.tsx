import { createBrowserRouter } from 'react-router'
import { HomePage } from '../features/home/HomePage'
import { PracticePage } from '../features/practice/PracticePage'

export const router = createBrowserRouter([
  { path: '/', element: <HomePage /> },
  { path: '/practice', element: <PracticePage /> },
])
