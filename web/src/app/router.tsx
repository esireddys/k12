import { createBrowserRouter } from 'react-router'
import { WelcomePage } from '../features/auth/WelcomePage'
import { ManageFamilyPage } from '../features/family/ManageFamilyPage'
import { WhoIsPracticingPage } from '../features/family/WhoIsPracticingPage'
import { ChooseCoursePage } from '../features/practice/ChooseCoursePage'
import { KidHomePage } from '../features/practice/KidHomePage'
import { SessionPage } from '../features/practice/SessionPage'
import { TestsPage } from '../features/practice/TestsPage'
import { TopicsPage } from '../features/practice/TopicsPage'
import { ReviewQueuePage } from '../features/review/ReviewQueuePage'
import { ReviewQuestionPage } from '../features/review/ReviewQuestionPage'

export const router = createBrowserRouter([
  { path: '/', element: <WelcomePage /> },
  { path: '/kids', element: <WhoIsPracticingPage /> },
  { path: '/family', element: <ManageFamilyPage /> },
  { path: '/course', element: <ChooseCoursePage /> },
  { path: '/home', element: <KidHomePage /> },
  { path: '/practice/topics', element: <TopicsPage /> },
  { path: '/practice/tests', element: <TestsPage /> },
  { path: '/practice/session', element: <SessionPage /> },
  { path: '/owner/review', element: <ReviewQueuePage /> },
  { path: '/owner/review/:questionId', element: <ReviewQuestionPage /> },
])
