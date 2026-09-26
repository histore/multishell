# Module Requirements: Internationalization & Localization

> **Specification Path**: `docs/requirements/modules/localization.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Internationalization (i18n) & Localization (l10n)** subsystem of MultiShell. It governs dynamic language switching across 6 European languages, OS culture autodetection, runtime dictionary bindings, and persistence.

---

## Module Overview
- **Module Name**: Internationalization & Localization
- **Scope Identifier**: `LOC` / `I18N`
- **Architecture Contract**: [`docs/architecture/modules/localization.md`](../../architecture/modules/localization.md)
- **Primary Domain Specialist**: `LocalizationSpecialist`

---

## Requirements

### `[REQ-LOC-001]` Dynamic Multi-Language UI (DE, EN, FR, ES, IT, PT) with Dropdown & Persistence

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** the UI to automatically adapt to my operating system language (with English fallback) and allow switching between German, English, French, Spanish, Italian, and Portuguese via a responsive dropdown selector with persistent storage across restarts.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an OS configured in German, French, Spanish, Italian, Portuguese, or English, the UI defaults to that language.
- [x] **AC-2**: **When** the user manually chooses a language from the Settings dropdown selector (`ComboBox`), **Then** all UI elements update immediately in real time without window reloads.
- [x] **AC-3**: **Then** the preference is persisted in `WorkspaceState.SavedLanguage` and restored on subsequent application launches.
- [x] **AC-4**: 0% hardcoded user strings; all labels, tooltips, dialogs, and menu headers are resolved via `LocalizationService`.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/localization.md`
- **Test Suite**: `MultiShell.Tests/LocalizationServiceTests.cs`

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-LOC-001` | Dynamic Multi-Language UI (DE, EN, FR, ES, IT, PT) | Functional | IMPLEMENTED | `v0.1.0` |
