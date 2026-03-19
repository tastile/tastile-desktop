// OBSOLETE: This file is no longer used.
// OAuth callback handling has been moved to the daemon (tastile-core).
// The daemon listens on localhost:1080/auth/callback which is registered in Supabase.
// 
// New authentication flow:
// 1. DesktopApp -> Daemon: POST /auth/oauth/start
// 2. Daemon opens system browser with Supabase OAuth URL
// 3. User authenticates with Google
// 4. Supabase redirects to http://localhost:1080/auth/callback (handled by daemon)
// 5. Daemon receives code and exchanges for session
// 6. DesktopApp polls Daemon: GET /auth/session

using System;

namespace TastileDesktop.Services;

[Obsolete("OAuth callback handling moved to daemon. Use CoreApiClient.StartBrowserAuthAsync() instead.", error: true)]
public class LocalOAuthServer { }

[Obsolete("Use AuthResult instead.", error: true)]
public class OAuthCallbackResult { }
