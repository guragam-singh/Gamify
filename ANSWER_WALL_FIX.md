# Answer-wall evaluation fix

## Root causes

Local question JSON was parsed as a backend Quiz because both formats contain
`questions`. Conversion to backend QuizQuestion discarded local correctIndex,
setting every key to -1. Valid answer gates therefore evaluated as incorrect.

Offline SubmitAnswer also deducted a life, then GameManager.RecordWrongAnswer
deducted another after the wall ram. Correct-answer counts were similarly doubled.

## Targeted changes

- QuestionData.cs: require a nonempty quiz_id before using the backend conversion;
  local wrapper and raw-array formats retain their answer keys and explanations.
- QuestionManager.cs: local submission evaluates only; GameManager retains sole
  ownership of the visible score, correct count and life changes after ram resolution.
  Reject invalid question/option selection and missing local keys.
- Existing correct-wall break/progression, wrong-wall recoil/retry and results
  routines are retained. A correct answer costs no life; an incorrect answer costs
  exactly one life. Starting lives remain three.

Backup: `C:\Users\Hp\AppData\Local\Temp\opencode\Gamify-before-answer-fix-20261011.zip`.

## Verification

Parser check: all five local keys preserved (1,3,0,1,2); synthetic backend quiz
remains server-authoritative (key -1). No live-backend request was tested.

Actual Play Mode verification runs the existing answer confirmation, physical ram,
recoil, explanation, retry and next-checkpoint sequence. Traffic penalties are
temporarily isolated during the quiz assertions and restored afterward.
Confirmed: incorrect gate A at question 1 reduced lives 3 -> 2 and preserved
checkpoint/score; retrying correct gate B broke the wall and resumed driving to
question 2 with lives still 2 and exactly one correct answer counted. Correct
gate D at question 2 also broke and advanced to question 3, count=2, lives=2.
Two subsequent wrong attempts at question 3 reduced lives 2 -> 1 -> 0, retaining
the same question; results appeared only at zero, with no automatic restart.
Invalid option -1 was rejected. Unity was left in Edit Mode; no diagnostic code
was added to gameplay scripts. No new compilation/runtime errors were observed;
existing Unity AI subscription errors remain.
