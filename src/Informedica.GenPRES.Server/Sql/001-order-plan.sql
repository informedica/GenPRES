-- The clinical store: every order plan version, never changed. The identity columns are
-- authoritative, so a row whose JSON a release cannot read keeps its place and its identity.
-- The order plan itself, the patient it was signed on included, is the JSON Dto of the
-- domain's OrderPlanVersion, under the JSON structure version it was written with.
-- signed_at holds Unix milliseconds; the JSON holds the full ticks. A readable entry takes
-- every value from the JSON; the columns serve the entry that cannot be read.
create table order_plan (
    id                     integer primary key,
    version_id             text not null unique,
    no                     integer not null,
    patient_id             text not null,
    base                   text null,        -- the version_id it was built on
    signed_by_user_id      text not null,
    signed_by_display_name text not null,
    signed_at              integer not null, -- unix ms
    verified               integer not null, -- 0 | 1: the platform's reading at the challenge
    json_version           integer not null, -- the JSON structure version of plan
    plan                   text not null,    -- json: OrderPlanVersion.Dto
    unique (patient_id, no)                  -- also the index for loading by patient
);
