# Contributing to VectraLang

Thank you for your interest in contributing to VectraLang! This document outlines the conventions and workflows used in this project. Please read it before opening a pull request.

---

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Branch Strategy](#branch-strategy)
- [Commit Message Conventions](#commit-message-conventions)
- [Pull Request Process](#pull-request-process)
- [Release & Versioning](#release--versioning)

---

## Code of Conduct

This project is committed to providing a welcoming and respectful environment for everyone. Contributors are expected to:

- Use inclusive and considerate language
- Respect differing viewpoints and experiences
- Accept constructive feedback graciously
- Focus on what is best for the project and community

Harassment, personal attacks, or exclusionary behavior of any kind will not be tolerated. If you experience or witness unacceptable behavior, please open a private issue or reach out to the maintainer directly.

---

## Branch Strategy

VectraLang uses a two-branch model:

| Branch   | Purpose                                       |
|----------|-----------------------------------------------|
| `dev`    | Active development and prerelease integration |
| `master` | Stable production releases only               |

### Working Branches

All new work should be done on a feature branch created from `dev`:
```bash
git checkout dev
git pull origin dev
git checkout -b feature/your-feature-name
```

Branch naming conventions:

| Prefix      | Use case                                   |
|-------------|--------------------------------------------|
| `feature/`  | New functionality                          |
| `fix/`      | Bug fixes                                  |
| `chore/`    | Maintenance, tooling, dependencies         |
| `docs/`     | Documentation only                         |
| `refactor/` | Code restructuring without behavior change |

#### Rules

- Never commit directly to `dev` or `master` - all changes must come through a pull request
- Never open a PR targeting `master` directly - master is only updated through the release process
- Feature branches should be short-lived and focused on a single concern

## Commit Message Conventions

VectraLang uses [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) to drive automated versioning and release notes. All commit messages must follow this format:
```
<type>(<scope>): <short description>

[optional body]

[optional footer]
```

### Types

| Type        | When to use                                             | Version bump |
|-------------|---------------------------------------------------------|--------------|
| `feat`      | A new feature                                           | Minor        |
| `fix`       | A bug fix                                               | Patch        |
| `chore`     | Tooling, dependencies, build changes                    | None         |
| `docs`      | Documentation only                                      | None         |
| `refactor`  | Code change that neither fixes a bug nor adds a feature | None         |
| `test`      | Adding or updating tests                                | None         |
| `perf`      | Performance improvement                                 | Patch        |
| `ci`        | CI/CD pipeline changes                                  | None         |

### Breaking changes

To signal a breaking change, add `BREAKING CHANGE:` in the commit footer, or append `!` after the type:
```
feat!: redesign bytecode instruction set

BREAKING CHANGE: opcode format has changed; existing .vbc files are not compatible.
```
Breaking changes trigger a **major** version bump and are subject to review by the project maintainers.

### Examples
```
feat(parser): add support for generic type parameters
fix(lexer): correctly handle escaped string literals
chore(deps): update NUnit to 4.3.0
docs(readme): add installation instructions
refactor(binder): extract symbol resolution into dedicated service
```

---

## Pull Request Process

Please follow the following guidelines when submitting a pull request:

### Before opening a PR
- Make sure your branch is up to date with `dev`
- Ensure all tests pass locally
- Keep your changes focused – one concern per PR

### Opening the PR
- All PRs must target the `dev` branch
- Fill out the PR template completely – it exists for a reason
- Use a clear, descriptive title that follows the same Conventional Commits format as your commit messages
- Reference any related issues in the PR description

### Merge Policy
- PRs are merged using **squash** only – this keeps the `dev` commit history and ensures each merged PR produces a single well-formed commit
- The squashed commit message should conform to the Conventional Commits format
- PRs require at least one approving review before merge

---

## Release & Versioning

VectraLang uses [semantic-release](https://semantic-release.gitbook.io/) to fully automate versioning and publishing. **You do not manually manage version numbers or changelogs.**

### How it works

| Branch   | Channel          | Example version |
|----------|------------------|-----------------|
| `dev`    | `dev` prerelease | `1.2.0-dev.1`   |
| `master` | stable release   | `1.2.0`         |

When a PR is merged into `dev`, semantic-release analyzes the commit messages and automatically:

1. Determines the appropriate version bump (major/minor/patch)
2. publishes a prerelease to the `dev` channel
3. Creates a GitHub prerelease with generated notes

When `dev` is promoted to `master` (via the maintainer), semantic-release publishes the stable release.

### What This Means for Contributors

- Your commit message types **directly determine the version bump** – Write them carefully
- Release notes are generated from your commit messages – a clear, well-scoped message is visible to everyone who consumes the project
- Do not include version bumps, changelog edits, or release tags in your PRs

---

*Thanks again for contributing. Every improvement, however small, helps move VectraLang forward.*