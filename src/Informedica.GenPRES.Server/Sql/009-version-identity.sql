-- Migration 9: whom a version names. A version signed in a Session with an identified patient
-- carries the patient's name and birthdate in the record's own columns, beside the plan whose
-- JSON is unchanged, so that a version read later names its patient on its own, readable or
-- not. All four present or all four null: null for a version signed without an identity and
-- for every row from before, which reads as naming none. The columns were added later and
-- cannot carry the check the other identity columns have, so the loader holds them to it.
alter table order_plan add column patient_name text null;
alter table order_plan add column birth_year integer null;
alter table order_plan add column birth_month integer null;
alter table order_plan add column birth_day integer null;
