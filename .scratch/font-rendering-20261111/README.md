# Debug record: colored fringes around queue text

Date: 2026-11-11

The reported screenshot shows the manager's Đơn lưu trú queue. The running application is served from `D:/DormPrj/Dorm-master`; its original CSS matched the workspace copy at `E:/DormPrj` byte for byte.

The actual queue markup and stylesheet reproduced the same red/cyan text edges in Windows Chromium. Vietnamese text contained no replacement characters and no labels were clipped. Changing the font family, weight, text-rendering or webkit font-smoothing did not remove the symptom. Disabling LCD text in an isolated browser, or placing the queue on a dedicated compositor layer, eliminated the fringes. The latter preserves the existing fonts, content, colors and layout and is scoped to the queue.

The fix adds `will-change: transform` to `.request-queue` in the active application and the matching workspace copy. No transform or animation is applied.

`verify.cjs` is a local diagnostic harness, not an application dependency. It uses the real CSS and the queue's rendered markup structure. Its pixel check failed before the fix and passed afterwards in Chrome and Edge at device scale factors 1, 1.25 and 1.5. After checks also compare the text and element bounds to the recorded baseline and require the live site's CSS to match the reviewed file. All six combinations passed. Browser errors were absent.

`before-*.png`, `after-*.png` and JSON results are retained here as debug evidence. The initial browser probes live in `E:/DormPrj/.scratch/font-rendering-20261111`. No debug instrumentation was added to application JavaScript. Including fractional display scales in visual review would have caught this defect earlier.
