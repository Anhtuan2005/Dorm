# ADR 0006: Use server-side Gemini for student help

Status: Accepted

Date: 2026-11-11

## Context

The student help dialog previously returned keyword-matched demo answers in JavaScript. The requested integration uses a Google Gemini API key, without placing credentials in browser assets or giving the model mutation rights.

## Decision

Add a student-authorized, antiforgery-protected POST endpoint and a typed HTTP service calling Gemini `generateContent`. Store `Chatbot:ApiKey` in .NET User Secrets during development or `Chatbot__ApiKey` in the deployment environment. Send it only in the provider header. Keep the model configurable; the initial model is `gemini-3.5-flash-lite`.

Provide server-owned instructions describing implemented housing, billing, maintenance and account workflows. The model receives the question and at most six prior messages. It does not receive student records or query SQL Server. This is functional guidance, not document retrieval or an authoritative source for regulations. The UI identifies AI responses and does not invent source citations.

Enforce input lengths, a per-user request limit and bounded timeouts. Return safe Vietnamese error messages without provider error bodies or credentials. Conversation history stays in browser memory for the current page; no new database tables are needed.

## Consequences

- The existing SQL Server schema and business transactions remain unchanged.
- Questions and short conversation context are sent to Google; automated student data retrieval is not enabled.
- Prompt instructions follow the existing domain ADRs, including the separate move-in handover.
- Quota, authentication and network failures are visible as retryable errors, never disguised as successful demo answers.
- HTTP contract and error paths can be tested without an external service or real key.

## Follow-up, 2026-11-17

`StudentHelpChatbotService` now resolves policy and document questions through a local catalog of approved excerpts. A match returns the document title, section, optional HTTPS URL and an application action. Development can fall back to clearly labeled examples. Missing sources lead to Ban quản lý. Personal balance queries open the authorized invoice page.

The Gemini client remains available for supported app workflows. The service maps each workflow to a server-owned question, so raw questions, previous turns and document excerpts are kept inside NẾP KTX. The browser keeps only the conversation shown in the current chat panel. The private catalog is read when the process first needs it, through `App_Data/chatbot-knowledge.json`, with a restart after publication or correction. No SQL migration is needed for this addition.

## Follow-up, 2026-11-18

Exact question aliases were too restrictive for normal conversation: both an unrelated question and “Có phòng nào còn phòng k” received the missing-document answer. The workflow router now recognizes Vietnamese phrases within a question, resolves the corresponding catalog question when available, and otherwise sends the same server-owned prompt to Gemini. Room availability opens the authenticated Tìm phòng page without claiming a live count. Policy and price questions still require a matching excerpt; unrelated questions explain scope, while unclear housing questions request clarification. These replies require neither a provider call nor access to student records.
