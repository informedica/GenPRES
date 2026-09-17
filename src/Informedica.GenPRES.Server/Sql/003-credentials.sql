-- Migration 3 (plan 516 step 7): the credential of a person, the confirmation code a launch
-- suspended into enrolment mails, and the attempts bound to it. Append-only like every other
-- table: a credential is a series of events and the newest one is the credential.

-- Rules 24, 25 and 37. One row per change: the PIN set, a wrong entry counted, the lock
-- reached, a right entry clearing the count. The row carries the credential as it stands
-- after the event, so a load reads one row and no history has to be replayed. The PIN itself
-- is never here: only its PBKDF2 derivation and the salt of that derivation.
create table credential_event (
    id            integer primary key,
    user_id       text not null,
    event         text not null,     -- pin-set | wrong | locked | right
    pin_salt      blob null,         -- null: a credential without a PIN
    pin_hash      blob null,
    wrong_count   integer not null,
    locked_until  integer null,      -- unix ms; a delay, not a state
    at            integer not null,
    check ((pin_salt is null) = (pin_hash is null))
);
create index ix_credential_user on credential_event (user_id, id);

-- Rule 27. The six-digit code mailed when a launch suspends into enrolment: never the code,
-- only the mac of it. One live code per person; the newest unspent row within its lifetime.
create table confirmation_code (
    id            integer primary key,
    user_id       text not null,
    mail_address  text not null,
    code_mac      blob not null,
    expiry        integer not null,
    at            integer not null
);
create index ix_code_user on confirmation_code (user_id, id);

-- A wrong code, one row each: the tries of a code are counted, and the third voids it.
create table code_try (
    id      integer primary key,
    code_id integer not null references confirmation_code (id),
    at      integer not null
);
create index ix_code_try on code_try (code_id);

-- The code is spent: the PIN was set, the tries ran out, or the last attempt bound to it was
-- dropped. A spent code is never the live one again.
create table code_spent (
    code_id integer primary key references confirmation_code (id),
    at      integer not null
);

-- UC-2. A launch suspended at the PIN question: what the open needs once the PIN is set, and
-- the key of the browser that made the attempt, so the Session opens on that browser's key.
-- No lifetime of its own: it lives as long as the code it is bound to.
create table enrolment (
    attempt      text primary key,
    user_id      text not null,
    login        text not null,
    display_name text not null,
    patient_id   text not null,
    public_key   text not null,
    at           integer not null
);
create index ix_enrolment_user on enrolment (user_id);

-- The attempt was abandoned, or the PIN was set and every attempt of that person dropped with
-- it. A dropped attempt is never found again.
create table enrolment_dropped (
    attempt text primary key references enrolment (attempt),
    at      integer not null
);
