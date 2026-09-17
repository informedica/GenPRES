-- One row per Launch, written before the identity hop; what its callback came to is a second
-- row, written once. Both stay: past the Launch's expiry they load as absent.
create table launch_record (
    nonce       text primary key,
    state       text not null unique,   -- the callback finds the record by it
    patient_id  text not null,
    public_key  text not null,          -- the browser's key; a repeat with it is a retry
    expiry      integer not null        -- unix ms, the Launch's own
);

create table launch_outcome (
    nonce       text primary key references launch_record (nonce),
    outcome     text not null,          -- opened | refused:<word> | enrolling
    session_id  text null,              -- the Session an open named
    attempt     text null,              -- the attempt a suspended launch named
    at          integer not null        -- unix ms
);

-- One row per open, never changed. Only the newest row of a login can be its open Session,
-- and it is unless an ending names it: newest by this id, never by a clock, and chosen
-- before the ending is read, so that an older row never comes back when a newer one ends.
create table session (
    id             integer primary key,  -- the engine's own order of the opens
    session_id     text not null unique,
    login          text null,
    user_id        text null,
    user_display   text null,
    user_role      text null,
    patient_id     text null,
    key_thumbprint text null,            -- the key the browser signs with
    opened_at      integer not null      -- unix ms
);
create index ix_session_login on session (login, id);

-- What the Session opened with and the token that names it: written at the open, at a version
-- opened and at a commit. The newest row counts. version_id is what it opened with, a version
-- that can be read; head_id is the head it saw, whatever its case, so a Session that opened on
-- a head it cannot read opens from nothing and still holds that head. Both name order_plan
-- rows, from which the loader rebuilds the head. The patient is the JSON Dto of the patient
-- data the Session shows, under its structure version, null when it opened on none.
create table session_opened_with (
    id           integer primary key,
    session_id   text not null references session (session_id),
    version_id   text null,              -- null: from nothing
    head_id      text null,              -- null: the patient had no order plan version
    opened_token text null,              -- null: a Session without one
    json_version integer null,           -- the JSON structure version of patient
    patient      text null,              -- json: the patient data as shown
    at           integer not null,       -- unix ms
    check ((patient is null) = (json_version is null))
);

-- A request from the Session, one row each. All stay; the loader reads the newest.
create table session_seen (
    id         integer primary key,
    session_id text not null references session (session_id),
    at         integer not null          -- unix ms
);

-- The endings that are acts. A supersession is no row: it is read off a newer session row of
-- the same login.
create table session_ending (
    session_id text primary key references session (session_id),
    ending     text not null,            -- closed | wrong-pin-limit | unreadable
    at         integer not null          -- unix ms
);

-- The User acknowledged the ending; it is never told again.
create table session_acknowledged (
    session_id text primary key references session (session_id),
    at         integer not null          -- unix ms
);
