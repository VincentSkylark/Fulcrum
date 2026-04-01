# Task 1: Implement User Authentication

## Overview
Add JWT-based authentication system with secure login and registration endpoints.

## Implementation Approach

1. **Set up JWT library and configuration**
   - Install `PyJWT` library
   - Create `src/auth/config.py` with JWT settings (secret, expiry, algorithm)
   - Store secret in environment variable `JWT_SECRET`

2. **Create user database schema**
   - Create `src/models/user.py` with User model
   - Fields: id, email, password_hash, created_at, updated_at
   - Use bcrypt for password hashing

3. **Implement registration endpoint**
   - Create `POST /api/auth/register`
   - Validate email format and password strength
   - Hash password before storing
   - Return JWT token on success

4. **Implement login endpoint**
   - Create `POST /api/auth/login`
   - Verify credentials
   - Return JWT token on success

5. **Add middleware for protected routes**
   - Create `src/auth/middleware.py`
   - Extract Bearer token from Authorization header
   - Validate token and attach user to request

## Key Decisions

- **JWT vs Session**: JWT chosen for stateless authentication, easier scaling
- **Password hashing**: bcrypt with cost factor 12 for security
- **Token expiry**: 24 hours for balance between security and UX

## Edge Cases

- Duplicate email registration: Return 409 Conflict
- Invalid credentials: Return 401 with generic error message (don't reveal which field is wrong)
- Expired token: Return 401 with specific error code for client to refresh

## Testing Strategy

- Unit tests for password hashing/verification
- Unit tests for JWT generation/validation
- Integration tests for registration and login endpoints
- Test middleware with valid/invalid/missing tokens
