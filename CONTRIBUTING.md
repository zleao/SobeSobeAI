# Contributing to SobeSobe

Thank you for your interest in contributing to SobeSobe! This document provides guidelines and best practices for contributing to the project.

## Code of Conduct

By participating in this project, you agree to maintain a respectful and inclusive environment for all contributors.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a feature branch** from `main`
4. **Make your changes** following our coding standards
5. **Test your changes** thoroughly
6. **Submit a pull request**

## Development Setup

See [README.md](./README.md) for detailed setup instructions.

## Coding Standards

### Frontend (Angular)

- Follow the [Angular Style Guide](https://angular.dev/style-guide)
- Use standalone components
- Write unit tests for components and services
- Use TypeScript strict mode
- Run `npm run lint` before committing

### Backend (.NET)

- Follow [.NET Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use Minimal APIs for endpoints
- Write unit and integration tests
- Use async/await for I/O operations
- Run `dotnet format` before committing

## Commit Message Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks
- `perf`: Performance improvements

### Examples

```
feat(game): implement trump selection logic

Add the ability for players to select trump suit before dealing cards.
Includes validation and state management.

Closes #42
```

```
fix(api): resolve connection timeout in multiplayer games

Increase gRPC connection timeout and add retry logic.
```

## Pull Request Process

1. **Update documentation** if you're changing functionality
2. **Add tests** for new features or bug fixes
3. **Ensure all tests pass** locally
4. **Update the README** if needed
5. **Request review** from maintainers

### PR Title Format

Use the same format as commit messages:
```
feat(scope): description
```

### PR Description Template

```markdown
## Description
Brief description of what this PR does.

## Related Issue
Closes #123

## Changes Made
- Change 1
- Change 2
- Change 3

## Testing
How has this been tested?

## Screenshots (if applicable)
Add screenshots for UI changes.
```

## Testing Guidelines

### Frontend Tests

- Write unit tests for all components and services
- Use Angular Testing Library
- Aim for 80%+ code coverage
- Test user interactions and edge cases

### Backend Tests

- Write unit tests for business logic
- Write integration tests for API endpoints
- Use xUnit for testing
- Mock external dependencies
- Aim for 80%+ code coverage

## Review Process

- All PRs require at least one approval
- Address review comments promptly
- Keep PRs focused and reasonably sized
- Be open to feedback and suggestions

## Questions?

If you have questions, please:
- Check existing issues and documentation
- Open a new issue with the `question` label
- Contact the maintainers

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to SobeSobe! 🎮
