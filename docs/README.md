# FreeSpeak Documentation

This folder contains technical documentation for the FreeSpeak application.

## Documentation Index

### Developer Guides

| Document | Description |
|----------|-------------|
| [DEVELOPER_GUIDE_BASE_COMPONENTS.md](DEVELOPER_GUIDE_BASE_COMPONENTS.md) | Guide to using `PostPageBase<TPost, TComment>` generic base component |
| [TESTING.md](TESTING.md) | Comprehensive testing guide with patterns, infrastructure, and best practices |
| [REPOSITORY_PATTERN.md](REPOSITORY_PATTERN.md) | Repository pattern implementation and data access architecture |
| [CONFIGURATION.md](CONFIGURATION.md) | Complete configuration guide including appsettings.json and options pattern |

### Architecture & Performance

| Document | Description |
|----------|-------------|
| [CACHING.md](CACHING.md) | Caching strategy including distributed cache, friendship cache, and compiled queries |
| [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md) | Database query optimizations, AsNoTracking, AsSplitQuery, DTO projections |

### Core Features

| Document | Description |
|----------|-------------|
| [NOTIFICATIONS.md](NOTIFICATIONS.md) | Notification system architecture, types, API, and UI integration |
| [THEME_SYSTEM.md](THEME_SYSTEM.md) | Theme system with 8 themes, CSS variables, and accessibility |
| [LOCALIZATION.md](LOCALIZATION.md) | Internationalization for 13 languages with translation workflow |
| [AUDIT_LOGGING_SYSTEM.md](AUDIT_LOGGING_SYSTEM.md) | Audit logging system for security and administrative actions |
| [AUDIT_LOGGING_QUICK_REFERENCE.md](AUDIT_LOGGING_QUICK_REFERENCE.md) | Quick reference guide for implementing audit logging |

### Group Post System

| Document | Description |
|----------|-------------|
| [GROUP_POST_SYSTEM.md](GROUP_POST_SYSTEM.md) | Database schema, entities, and post approval system |
| [GROUP_POST_QUICK_REFERENCE.md](GROUP_POST_QUICK_REFERENCE.md) | Quick reference for GroupPostService API methods |
| [GROUP_POST_EVENT_HANDLERS_USAGE.md](GROUP_POST_EVENT_HANDLERS_USAGE.md) | Usage guide for shared GroupPostEventHandlers service |

### System Features

| Document | Description |
|----------|-------------|
| [NOTIFICATION_CLEANUP.md](NOTIFICATION_CLEANUP.md) | Background notification cleanup service |
| [POST_DELETION_CLEANUP.md](POST_DELETION_CLEANUP.md) | Cascade delete strategy for post deletion |
| [POST_NOTIFICATION_MUTING.md](POST_NOTIFICATION_MUTING.md) | Per-post notification muting feature |

### Security

| Document | Description |
|----------|-------------|
| [SECURITY.md](SECURITY.md) | Comprehensive security guide with XSS, SQL injection, authorization, and OWASP compliance |

### Tools & Scripts

| Document | Description |
|----------|-------------|
| [POWERSHELL_SCRIPTS.md](POWERSHELL_SCRIPTS.md) | PowerShell scripts for validation and maintenance |

## Quick Links

- **New to the codebase?** Start with the [main README](../README.md)
- **Working with post pages?** See [DEVELOPER_GUIDE_BASE_COMPONENTS.md](DEVELOPER_GUIDE_BASE_COMPONENTS.md)
- **Working with groups?** See [GROUP_POST_SYSTEM.md](GROUP_POST_SYSTEM.md)
- **Setting up caching?** See [CACHING.md](CACHING.md) and [CONFIGURATION.md](CONFIGURATION.md)
- **Writing tests?** See [TESTING.md](TESTING.md)
- **Optimizing queries?** See [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md)
- **Understanding repositories?** See [REPOSITORY_PATTERN.md](REPOSITORY_PATTERN.md)
- **Security concerns?** See [SECURITY.md](SECURITY.md)
- **Adding translations?** See [LOCALIZATION.md](LOCALIZATION.md)
- **Working with themes?** See [THEME_SYSTEM.md](THEME_SYSTEM.md)
