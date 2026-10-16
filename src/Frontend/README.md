# JOMS Frontend

Standalone React + TypeScript application built with Vite. This project is separate from the .NET backend.

## Development

From this directory, create `.env.local` from `.env.example`, then run:

```powershell
npm install
npm run dev
```

Open the URL printed by Vite (normally `http://localhost:5173`). Set `VITE_API_URL` to the backend origin. The backend must allow the frontend origin in its CORS configuration.

Sign-in calls `POST /api/auth/login`, which checks username and password against the backend database. Passwords use salted PBKDF2 hashes; plaintext passwords are not stored. Development account creation requires a username, a password of at least six characters, and a display name. The account-creation form calls `POST /api/dev/users`, which is only available in the backend Development environment. Run the backend with `dotnet run` from `src\Backend` to use its Development launch profile. Production accounts must be provisioned through a trusted administrative process.

The role-based dashboard implements the order workflow:

- Customers submit orders and approve or request revisions on submitted designs.
- Admins assign submitted orders to CAD and customer-approved orders to Production.
- CAD users see their assigned orders and submit designs for customer approval.
- Production users start work, update the sequential Bag Generate, Production, Casting, Rhodium, and Finish Bag stages, then assign finished orders to Billing.
- Billing users complete final billing and mark orders dispatched.

CAD, Production, and Billing users only see orders currently assigned to them. Workflow actions generate persistent notifications for the relevant staff and customer.

## Notifications

The notification bell loads the authenticated user's notification history, subscribes to SignalR event `ReceiveNotification`, refreshes after reconnect, and supports marking one or all notifications as read.
