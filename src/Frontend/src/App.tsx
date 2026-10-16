import { useState } from "react";
import { NotificationBell } from "./components/NotificationBell";
import { OrderDashboard } from "./components/OrderDashboard";
import "./App.css";

const apiUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

type SignedInUser = {
  accessToken: string;
  role: string;
  displayName: string;
};

async function getApiError(response: Response): Promise<string | null> {
  const body = await response.text();
  if (!body) return null;

  try {
    const result = JSON.parse(body) as {
      error?: string;
      detail?: string;
      title?: string;
    };
    return result.error ?? result.detail ?? result.title ?? null;
  } catch {
    return body;
  }
}

function App() {
  const [role, setRole] = useState("Admin");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [signedInUser, setSignedInUser] = useState<SignedInUser | null>(null);
  const [isCreatingUser, setIsCreatingUser] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submitCredentials(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      if (isCreatingUser) {
        const createResponse = await fetch(`${apiUrl}/api/dev/users`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ username, password, displayName, role }),
        });
        if (!createResponse.ok) {
          const apiError = await getApiError(createResponse);
          if (createResponse.status === 404) {
            throw new Error(
              "The account-creation endpoint is unavailable. Start the backend in Development mode and restart it.",
            );
          }
          throw new Error(
            apiError ??
            `Account creation failed (${createResponse.status}).`,
          );
        }
      }

      const response = await fetch(`${apiUrl}/api/auth/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error("Username or password is incorrect.");
        }
        const apiError = await getApiError(response);
        throw new Error(apiError ?? `Login failed (${response.status}).`);
      }

      const result: SignedInUser = await response.json();
      setSignedInUser(result);
      setPassword("");
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Could not connect to the API.",
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">Jewelry Order Management</p>
          <h1>Notification center</h1>
        </div>
        {signedInUser && <NotificationBell accessToken={signedInUser.accessToken} />}
      </header>

      <section className="intro-card">
        <div>
          <p className="eyebrow">OMS · Real-time updates</p>
          <h2>Order notifications, all in one place.</h2>
          <p className="intro-copy">
            Connect with your API to view notification history and receive new
            order updates live.
          </p>
        </div>
        <div className="status-pill">
          <span className={signedInUser ? "status-dot online" : "status-dot"} />
          {signedInUser ? `Signed in as ${signedInUser.displayName} (${signedInUser.role})` : "Not connected"}
        </div>
      </section>

      <section className="connect-card">
        <div className="card-heading">
          <div>
            <p className="eyebrow">Development sign-in</p>
            <h2>Connect to your API</h2>
          </div>
          {signedInUser && (
            <button
              className="secondary-button"
              type="button"
              onClick={() => setSignedInUser(null)}
            >
              Disconnect
            </button>
          )}
        </div>
        <p className="help-copy">
          {isCreatingUser
            ? "Create a local development account, then sign in. This account-creation route is disabled outside Development."
            : "Sign in with your username and password. Your credentials are checked against the backend database."}
        </p>
        <form className="token-form" onSubmit={submitCredentials}>
          <label>
            Username
            <input
              autoComplete="username"
              minLength={3}
              maxLength={100}
              required
              value={username}
              onChange={(event) => setUsername(event.target.value)}
            />
          </label>
          <label className="name-field">
            Password
            <input
              autoComplete={isCreatingUser ? "new-password" : "current-password"}
              minLength={isCreatingUser ? 6 : undefined}
              maxLength={256}
              required
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>
          {isCreatingUser && (
            <>
              <label>
                Display name
                <input
                  autoComplete="name"
                  minLength={1}
                  maxLength={150}
                  required
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                />
              </label>
              <label>
                Role
                <select value={role} onChange={(event) => setRole(event.target.value)}>
                  <option>Admin</option>
                  <option>Customer</option>
                  <option>CAD</option>
                  <option>Production</option>
                  <option>Billing</option>
                </select>
              </label>
            </>
          )}
          <button className="primary-button" disabled={loading} type="submit">
            {loading
              ? "Please wait…"
              : isCreatingUser
                ? "Create account & sign in"
                : "Sign in"}
          </button>
        </form>
        {error && (
          <p className="error-message" role="alert">
            {error}
          </p>
        )}
        <p className="api-address">
          API: <code>{apiUrl}</code>
        </p>
        {signedInUser && (
          <OrderDashboard accessToken={signedInUser.accessToken} role={signedInUser.role} />
        )}

        {!signedInUser && (
          <button
            className="account-mode-button"
            type="button"
            onClick={() => {
              setError(null);
              setIsCreatingUser((value) => !value);
            }}
          >
            {isCreatingUser ? "Already have an account? Sign in" : "Create a development account"}
          </button>
        )}
      </section>

      {!signedInUser && (
        <section className="empty-state">
          <span className="empty-icon" aria-hidden="true">↗</span>
          <h2>Your updates will appear here</h2>
          <p>Connect with a demo token to load notifications and go live.</p>
        </section>
      )}

      <footer>JOMS · Notifications are securely scoped to the signed-in user.</footer>
    </main>
  );
}

export default App;
