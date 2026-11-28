# Branding and voice

The look and voice of the app, and where each piece lives in code. The brand should feel like a friendly
coach to a 3rd grader and a serious tool to a parent: small bright steps, every day, on real test questions.

## Where things live

| What | Where | Rule |
|---|---|---|
| App name, theme colour | `src/shared/config/brand.ts` | The only place the name is written. Header, tab title, PWA manifest and Capacitor app name all read it. |
| Colours, fonts, radii, type sizes | `src/shared/theme/tokens.css` | Components use semantic tokens (`--accent`, `--reward`, `--miss` ...), never a hex value or a `--brand-*` primitive. |
| Fonts | `src/shared/theme/fonts.ts` | Bundled with the app (no Google Fonts at runtime: no kid IPs to a third party, works offline). |
| Icons | `src/shared/ui/Icon.tsx` | One set. Add new icons there in the same style. |

To rename the app: change `appName` and `shortName` in `brand.ts`. Check trademarks, App Store names and
domains first. Never put a state, exam or standards name in the app name. The Capacitor `appId` can't change
after the first App Store release, so settle the name before that.

## Palette

| Token | Use |
|---|---|
| `--accent` (blue `#1F5BD6`) | Primary actions and links. |
| `--reward`, `--reward-soft`, `--reward-ink` (sun `#FFB020`) | Stars, streaks and celebrations, on kid screens only. Never for plain buttons, never as text on white. |
| `--good*` (mint) | Correct. Always with a check icon or a word. |
| `--miss*` (coral) | "Not yet" and mistakes. Coral, never red, never an X. |
| `--danger*` | Owner tools only (a real problem with a question). Kids never see it. |
| `--ink`, `--ink-quiet`, `--bg`, `--surface`, `--border` | Text and surfaces. |

Colour is never the only signal: every state also has an icon or a word. Text pairs meet WCAG AA.
The parent area stays calmer: ink headers, no stars or sun colour.

## Type

- **Nunito** (`--font-display`, 600 to 900): headings, buttons, chips, numbers on badges.
- **Atkinson Hyperlegible** (`--font-body`, 400 and 700): body copy and any question text we render.
  1, l and I look different, which matters in math.
- Sizes: kid body 18 px or more (`--text-kid-body`), answer buttons 22 to 24 px (`--text-kid-answer`),
  printed pictures at 16 px text or more. Parent screens can use 16 px (`--text-parent-body`).
  Use tabular numbers for scores.

## Voice

**To kids:** short, warm, concrete, never shaming. Talk about the work, not the child.

- Say "Not quite" or "Not yet", never "Wrong", "Incorrect" or "Fail".
- Give a next step: "Try once more", "Let's fix the 3 you missed".
- Say what they did before a percentage: "You answered 5 of 32", not "6%" alone.
- Tell them how to type an answer, not that they did it wrong: "Type a whole number, like 24."
- No exclamation-mark praise for low scores, and no comparison with other kids.

**To parents:** plain and factual. Say where questions come from ("2022 released test"). No hype and no
"AI-powered" claims. Exam and state names come from curriculum data, never from code.

| Where | Avoid | Use |
|---|---|---|
| Miss in practice | Not quite. The answer is D. | Not quite. Try once more. |
| Second miss | Look at the question again. | The answer is D, 7 stars. 14 shared by 2 posters is 7 each. |
| Low score | Keep practicing! 6%. | You answered 5 of 32. Let's fix the 3 you missed. |
| Bad number input | That doesn't look like a integer answer. | Type a whole number, like 24. |

## Icons and imagery

- Rounded line icons: 24 px grid, 2.2 px stroke, round caps and joins. Use the `-filled` version for an
  "on" state (picked, flagged, earned).
- Stars use `star-filled` on `--reward`; streaks use `sun`.
- Kid avatars are a coloured initial (later, friendly animal faces). Never photos.
- No stock photos. Pictures in questions come only from the printed tests.
- A logo and app icon are not chosen yet.
