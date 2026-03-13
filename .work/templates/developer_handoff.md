# Developer Handoff

## Ticket Information
Ticket ID: {{ticket_id}}
Title: {{ticket_title}}
Source: {{jira_or_trello_url}}

Prepared By: Taskmaster Agent
Date: {{date}}

---

# 1. Feature Summary

Brief description of the feature or change.

Example:
Implement password reset functionality allowing users to request
a reset token via email and update their password.

---

# 2. Business Objective

Why this change is needed.

Example:
Users must be able to reset their password without contacting support.

---

# 3. Requirements

List all functional requirements.

1. User can request a password reset.
2. System generates a reset token.
3. Token expires after 30 minutes.
4. User can set a new password using the token.

---

# 4. Acceptance Criteria

Define conditions that must be satisfied for the ticket to be complete.

- Endpoint returns HTTP 200 on success.
- Invalid token returns HTTP 400.
- Expired token returns HTTP 401.
- Password must meet security policy.

---

# 5. Technical Context

Relevant architecture or system constraints.

Example:
- ASP.NET Core API
- Authentication handled by ASP.NET Identity
- Password reset handled by UserManager

---

# 6. Files Likely Impacted

List likely files to modify or extend.

Example:

Controllers:
- UserController.cs

Services:
- PasswordResetService.cs

Repositories:
- UserRepository.cs

---

# 7. Suggested Implementation Approach

Optional guidance for the Developer agent.

Example:

1. Add endpoint POST /api/password-reset/request
2. Generate secure reset token
3. Send email with reset link
4. Add endpoint POST /api/password-reset/confirm

---

# 8. API Changes

If new endpoints are required.

Example:

POST /api/password-reset/request

Request:
{
  "email": "user@example.com"
}

Response:
{
  "message": "Reset email sent"
}

---

# 9. Data Model Changes

If database changes are needed.

Example:

Table: PasswordResetTokens

Columns:
- Id
- UserId
- Token
- ExpiryDate

---

# 10. Dependencies

List systems or components involved.

Example:

- Email service
- Authentication service
- Database

---

# 11. Edge Cases

Important scenarios to handle.

Example:

- Email does not exist
- Token expired
- Multiple reset requests
- Invalid token format

---

# 12. Testing Expectations

Developer must include:

- Unit tests for service logic
- Validation tests
- Error handling tests

Integration tests handled by Tester agent.

---

# 13. Definition of Done

The task is complete when:

- Feature implemented
- Unit tests written and passing
- Code compiles
- Acceptance criteria satisfied

---

# 14. Notes

Additional context from the ticket or stakeholders.

---

# 15. Open Questions

List unclear points.

Example:

- Should reset tokens be single-use?