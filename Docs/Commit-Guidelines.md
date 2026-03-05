# Commit Guidelines

To ensure a standardized and readable commit history, we use a combination of **Conventional Commits** and **Gitmoji**.

## Workflow

We have integrated tooling to help you generate compliant commit messages.

### Using the CLI Helper (Recommended)

Instead of `git commit`, use the following command to start the interactive commit wizard:

```bash
npm run commit
```

This will prompt you for the type of change, scope, emoji, and description, ensuring the format is correct.

### Manual Commit Format

If you prefer to commit manually, use the following format:

```
<emoji> <type>(<scope>): <subject>

<body>

<footer>
```

**Example:**

```
:sparkles: feat(auth): add login with google

Added Google OAuth provider to the authentication service.
Resolves #123
```

## Commit Types & Gitmojis

| Type | Emoji | Code | Description |
|---|---|---|---|
| **feat** | ✨ | `:sparkles:` | A new feature |
| **fix** | 🐛 | `:bug:` | A bug fix |
| **docs** | 📝 | `:memo:` | Documentation only changes |
| **style** | 💄 | `:lipstick:` | Changes that do not affect the meaning of the code (white-space, formatting, etc) |
| **refactor** | ♻️ | `:recycle:` | A code change that neither fixes a bug nor adds a feature |
| **perf** | ⚡️ | `:zap:` | A code change that improves performance |
| **test** | ✅ | `:white_check_mark:` | Adding missing tests or correcting existing tests |
| **build** | 📦 | `:package:` | Changes that affect the build system or external dependencies |
| **ci** | 🎡 | `:ferris_wheel:` | Changes to our CI configuration files and scripts |
| **chore** | 🔨 | `:hammer:` | Other changes that don't modify src or test files |
| **revert** | ⏪️ | `:rewind:` | Reverts a previous commit |

## Validation

We use `husky` and `commitlint` to validate commit messages before they are created. If your commit message does not follow the convention, the commit will be rejected with an error message explaining what is wrong.

### CI/CD Pipeline

The CI pipeline is configured to validate commit messages on Pull Requests. Ensure your PR title also follows this convention if you squash-merge.
