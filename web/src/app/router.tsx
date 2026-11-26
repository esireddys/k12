import { createBrowserRouter } from 'react-router'
import { HomePage } from '../features/home/HomePage'
import { PracticePage } from '../features/practice/PracticePage'
import { ReviewQueuePage } from '../features/review/ReviewQueuePage'
import { ReviewQuestionPage } from '../features/review/ReviewQuestionPage'

export const router = createBrowserRouter([
  { path: '/', element: <HomePage /> },
  { path: '/practice', element: <PracticePage /> },
  { path: '/owner/review', element: <ReviewQueuePage /> },
  { path: '/owner/review/:questionId', element: <ReviewQuestionPage /> },
])
