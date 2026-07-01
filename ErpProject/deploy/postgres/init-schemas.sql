-- ERP Module Schema Initialization
-- Executed automatically by PostgreSQL on first container startup.
-- Creates one schema per module for physical isolation.
-- Idempotent: safe to run multiple times.

CREATE SCHEMA IF NOT EXISTS billing;
CREATE SCHEMA IF NOT EXISTS crm;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS accounting;
CREATE SCHEMA IF NOT EXISTS expenses;
