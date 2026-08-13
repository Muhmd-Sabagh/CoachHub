# CoachHub Web

Angular 22 administration shell for CoachHub. Its responsive visual system is adapted from the MIT-licensed TailAdmin free Next.js dashboard template, while the implementation follows CoachHub's Angular feature architecture.

## Run locally

1. Start `CoachHub.API` on its HTTPS development port.
2. Run `npm start` in this directory. The development proxy forwards `/api` and `/health` to the API.
3. Open `https://127.0.0.1:65194` and sign in with a seeded administrator account.

The API base URL is configured in `src/environments`. Authentication state is kept in browser local storage, attached by the auth interceptor, and cleared on an unauthorized response.

## Commands

- `npm run build` creates a production build.
- `npm test -- --watch=false` runs the unit tests once.

## Structure

- `core`: authentication, configuration, localization, and the application shell.
- `shared`: reusable components, models, pipes, and utilities.
- `features`: route-owned screens and feature workspaces.

English and Arabic are available from the header or login screen. Changing language also updates the document direction between LTR and RTL.