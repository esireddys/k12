/** What the app keeps about kids, in plain words, wherever a parent manages them. */
export function PrivacyNote() {
  return (
    <details className="card" style={{ gap: 8 }}>
      <summary style={{ cursor: 'pointer', fontWeight: 800, fontSize: 18 }}>What we save about your kids</summary>
      <ul style={{ margin: 0, paddingLeft: 20, lineHeight: 1.6 }}>
        <li>A first name or nickname, a picture colour and a grade. Never an email, photo or location.</li>
        <li>Each answer your kid gives, whether it was right, and how long practice took, to show progress here.</li>
        <li>It is tied to your Google sign-in, and in the app only you can see it. Guests' practice is never saved.</li>
        <li>Removing a kid deletes their profile, answers and progress for good.</li>
      </ul>
    </details>
  )
}
