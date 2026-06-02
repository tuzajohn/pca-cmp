# PCA Change Management – common developer commands
# Run from the repo root. Requires .NET 10 SDK.

WEB := src/PCA.Web

# ── Setup ────────────────────────────────────────────────────────────────────

.PHONY: restore
restore:                          ## Restore NuGet packages + local dotnet tools
	dotnet restore
	dotnet tool restore

# ── Migrations ───────────────────────────────────────────────────────────────

.PHONY: migration-add
migration-add:                    ## Add migration: make migration-add NAME=InitialCreate
ifndef NAME
	$(error NAME is required. Usage: make migration-add NAME=<MigrationName>)
endif
	dotnet ef migrations add $(NAME) --project $(WEB) --startup-project $(WEB)

.PHONY: migration-remove
migration-remove:                 ## Remove the last migration
	dotnet ef migrations remove --project $(WEB) --startup-project $(WEB)

.PHONY: db-update
db-update:                        ## Apply all pending migrations to the database
	dotnet ef database update --project $(WEB) --startup-project $(WEB)

.PHONY: db-update-to
db-update-to:                     ## Roll forward/back to a specific migration: make db-update-to MIGRATION=<name>
ifndef MIGRATION
	$(error MIGRATION is required. Usage: make db-update-to MIGRATION=<MigrationName>)
endif
	dotnet ef database update $(MIGRATION) --project $(WEB) --startup-project $(WEB)

.PHONY: db-drop
db-drop:                          ## Drop the entire database (destructive!)
	dotnet ef database drop --force --project $(WEB) --startup-project $(WEB)

.PHONY: migrations-list
migrations-list:                  ## List all migrations and their applied status
	dotnet ef migrations list --project $(WEB) --startup-project $(WEB)

.PHONY: migrations-script
migrations-script:                ## Generate an idempotent SQL script for all migrations
	dotnet ef migrations script --idempotent --output migrations.sql --project $(WEB) --startup-project $(WEB)
	@echo "SQL script written to migrations.sql"

# ── Build / Run ──────────────────────────────────────────────────────────────

.PHONY: build
build:                            ## Build the solution
	dotnet build

.PHONY: run
run:                              ## Run the web application
	dotnet run --project $(WEB)

.PHONY: watch
watch:                            ## Run with hot reload
	dotnet watch --project $(WEB)

# ── Help ─────────────────────────────────────────────────────────────────────

.PHONY: help
help:                             ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) \
		| awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-20s\033[0m %s\n", $$1, $$2}'

.DEFAULT_GOAL := help
