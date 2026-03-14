# FreeSpeak Documentation

This folder contains technical documentation for the FreeSpeak application.

## Documentation Index

### Developer Guides

| Document | Description |
|----------|-------------|
| [DEVELOPER_GUIDE_BASE_COMPONENTS.md](DEVELOPER_GUIDE_BASE_COMPONENTS.md) | Guide to using `PostPageBase<TPost, TComment>` generic base component for post pages |
| [TESTING_PATTERNS.md](TESTING_PATTERNS.md) | Unit testing patterns, best practices, and infrastructure guide |
| [REPOSITORY_PATTERN.md](REPOSITORY_PATTERN.md) | Repository pattern implementation and data access architecture |
| [CONFIGURATION.md](CONFIGURATION.md) | Complete configuration guide including appsettings.json, environment variables, and options pattern |

### Architecture & Performance

| Document | Description |
|----------|-------------|
| [CACHING.md](CACHING.md) | Comprehensive caching strategy including distributed cache, friendship cache, and compiled queries |
| [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md) | Database query optimizations, AsNoTracking, AsSplitQuery, DTO projections, and performance metrics |

### Group Post System

| Document | Description |
|----------|-------------|
| [GROUP_POST_SYSTEM.md](GROUP_POST_SYSTEM.md) | Database schema and entity documentation for the group post system |
| [GROUP_POST_QUICK_REFERENCE.md](GROUP_POST_QUICK_REFERENCE.md) | Quick reference for GroupPostService API methods |
| [GROUP_POST_EVENT_HANDLERS_USAGE.md](GROUP_POST_EVENT_HANDLERS_USAGE.md) | Usage guide for the shared GroupPostEventHandlers service |
| [GROUP_POST_IMPLEMENTATION_SUMMARY.md](GROUP_POST_IMPLEMENTATION_SUMMARY.md) | Implementation summary including services and business logic |

### System Features

| Document | Description |
|----------|-------------|
| [NOTIFICATION_CLEANUP.md](NOTIFICATION_CLEANUP.md) | Background notification cleanup service architecture |
| [POST_DELETION_CLEANUP.md](POST_DELETION_CLEANUP.md) | Cascade delete strategy and cleanup order for post deletion |
| [POST_NOTIFICATION_MUTING.md](POST_NOTIFICATION_MUTING.md) | Per-post notification muting feature documentation |

### Security

| Document | Description |
|----------|-------------|
| [SECURITY_AUDIT_RESULTS.md](SECURITY_AUDIT_RESULTS.md) | XSS, SQL injection, and path traversal security audit results |
| [DOS_DDOS_AUDIT_RESULTS.md](DOS_DDOS_AUDIT_RESULTS.md) | DOS/DDOS vulnerability assessment and implemented protections |

### Testing

| Document | Description |
|----------|-------------|
| [FINAL_TESTING_CHECKLIST.md](FINAL_TESTING_CHECKLIST.md) | Manual testing checklist for Groups and Comments functionality |

## Quick Links

- **New to the codebase?** Start with the [main README](../README.md)
- **Working with post pages?** See [DEVELOPER_GUIDE_BASE_COMPONENTS.md](DEVELOPER_GUIDE_BASE_COMPONENTS.md)
- **Working with groups?** See [GROUP_POST_SYSTEM.md](GROUP_POST_SYSTEM.md)
- **Setting up caching?** See [CACHING.md](CACHING.md) and [CONFIGURATION.md](CONFIGURATION.md)
- **Writing tests?** See [TESTING_PATTERNS.md](TESTING_PATTERNS.md)
- **Optimizing queries?** See [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md)
- **Understanding repositories?** See [REPOSITORY_PATTERN.md](REPOSITORY_PATTERN.md)
- **Security concerns?** See [SECURITY_AUDIT_RESULTS.md](SECURITY_AUDIT_RESULTS.md)
