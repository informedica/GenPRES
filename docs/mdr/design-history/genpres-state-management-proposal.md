# GenPRES State Management Design

## Executive Summary

GenPRES will be developed as a stateless persistence service for EHR integration. The service maintains zero permanent patient data, operates through temporary sessions where patient context is provided by the EHR, and returns all modifications upon session completion. Clinical calculation capabilities are preserved while eliminating persistent data storage.

## Core Design Principle: Stateless Persistence

### Definition
Stateless Persistence implementation:
- No permanent patient storage: Patient data, orders, or clinical decisions persist only during active sessions
- Temporary working sessions: Clinical context maintained during prescription workflows
- Complete state transfer: All session modifications returned to EHR system
- Resource-only caching: Institutional rules, formularies, and product data cached for performance

### Service Boundary
```
Session Lifecycle:
EHR → [Start Session + Patient Data + Orders] → GenPRES Session
      ↓
Active Session: GenPRES ←→ Clinician Interface (CRUD Operations)
      ↓
EHR ← [Modified Orders + Clinical Decisions] ← Session End → [State Discarded]
```

GenPRES operates as a computational clinical service with temporary working memory.

## Architectural Design

### 1. EHR-Controlled Session Management

#### Session Initiation
```fsharp
type EhrSessionRequest = {
    SessionId: Guid
    EhrSystemId: string           // Identifying which EHR system
    EhrUserId: string            // EHR user reference (not stored)
    EhrPatientId: string         // EHR patient reference (not stored)
    PatientSnapshot: PatientData // Complete patient context for calculations
    ExistingOrders: EhrOrder[]   // Current patient orders from EHR
    AuthorizationContext: {      // EHR-provided user permissions and restrictions
        UserRole: ClinicalRole
        Permissions: Permission[]
        Restrictions: Restriction[]
        DepartmentAccess: string[]
        PatientAccessLevel: AccessLevel
    }
    ClinicalContext: {
        Department: string
        Institution: string
        ClinicalFlags: ClinicalFlag[]
        Urgency: UrgencyLevel
    }
    ResourceVersions: {          // Which cached resources to use
        FormularyVersion: string
        ProductDataVersion: string
        InstitutionalRulesVersion: string
    }
    SessionConfiguration: {
        TimeoutMinutes: int
        AutoSyncEnabled: bool
        MaxOrdersPerSession: int
    }
}

type GenPresSession = {
    SessionId: Guid
    EhrReference: EhrSystemId * EhrUserId * EhrPatientId  // References only
    AuthorizationContext: AuthorizationContext           // User permissions from EHR
    PatientContext: PatientData      // Temporary clinical context
    WorkingOrders: Map<OrderId, Order>    // Active order workspace
    EhrOrderMapping: Map<EhrOrderId, GenPresOrderId>     // Order reference mapping
    ClinicalDecisions: ClinicalDecision[] // Treatment decisions made during session
    CreatedAt: DateTime
    LastActivity: DateTime
    ExpiresAt: DateTime
    AutoSync: bool
}
```

#### Session Operations
```fsharp
type SessionOperation =
    | CreateOrder of OrderRequest
    | ModifyOrder of OrderId * OrderModification
    | DeleteOrder of OrderId
    | SelectScenario of OrderId * ScenarioId
    | ValidateOrderSet
    | CalculateTreatmentPlan
    | SyncToEhr                   // Manual sync during session
    | FinalizeSession             // Complete and transfer all changes

type SessionResponse = {
    SessionId: Guid
    UpdatedOrders: Map<OrderId, Order>
    CalculatedScenarios: Map<OrderId, OrderScenario[]>
    ValidationResults: ValidationResult[]
    ClinicalAlerts: ClinicalAlert[]
    TreatmentPlanUpdate: TreatmentPlan option
    SyncStatus: SyncStatus
}
```

### 2. Modified Client Architecture

#### Stateless Client State
```fsharp
type ClientState = {
    // NO PERSISTENT PATIENT DATA
    ActiveSession: EhrSession option     // Temporary session context
    WorkingOrders: Map<OrderId, Order>   // Session-scoped orders
    
    // UI State Only
    Page: Global.Pages
    UISettings: UIConfiguration
    
    // Cached Resources (Non-Patient Data)
    CachedFormulary: Deferred<Formulary>
    CachedProducts: Deferred<Product list>
    CachedRules: Deferred<InstitutionalRules>
    
    // Real-time Session Data
    CalculationResults: Map<OrderId, OrderScenario[]>
    ValidationStatus: ValidationResults
    TreatmentPlan: TreatmentPlan option  // Session-scoped only
}

type ClientMessage =
    | EhrSessionStarted of EhrSessionRequest
    | EhrSessionClosed of SessionId
    | OrderOperation of SessionId * OrderOperation
    | SessionSyncRequested of SessionId
    | SessionFinalized of SessionId
    // Remove all patient-specific persistence messages
```

#### Session-Aware UI Components
```fsharp
let SessionAwarePatientView props =
    match props.activeSession with
    | None -> 
        // Display "Waiting for EHR session" state
        WaitingForEhrView()
    | Some session ->
        // Display patient context from session
        PatientView({
            patient = session.PatientContext
            readonly = true  // Patient data managed by EHR
            orders = session.WorkingOrders
            onOrderOperation = props.onOrderOperation
        })
```

### 3. Server Session Management

#### Session Store
```fsharp
type SessionStore = {
    ActiveSessions: Map<SessionId, GenPresSession>
    SessionsByEhr: Map<EhrSystemId, SessionId[]>
    ExpirationQueue: PriorityQueue<SessionId, DateTime>
}

let sessionManager = {
    CreateSession: EhrSessionRequest -> Async<Result<SessionResponse, string[]>>
    GetSession: SessionId -> Async<GenPresSession option>
    UpdateSession: SessionId -> SessionOperation -> Async<Result<SessionResponse, string[]>>
    SyncSession: SessionId -> Async<Result<EhrSyncData, string[]>>
    FinalizeSession: SessionId -> Async<Result<FinalizedOrders, string[]>>
    CloseSession: SessionId -> Async<Result<unit, string[]>>
    CleanupExpiredSessions: unit -> Async<int>  // Returns number cleaned
}
```

#### Session Processing Pipeline
```fsharp
let processSessionOperation sessionId operation = async {
    let! sessionOpt = SessionStore.getSession sessionId
    match sessionOpt with
    | None -> return Error [| "Session not found or expired" |]
    | Some session ->
        // Validate operation against user authorization context
        let! authResult = validateOperation operation session.AuthorizationContext
        match authResult with
        | Error authErrors -> return Error authErrors
        | Ok _ ->
            // Apply operation to session state
            let! updatedSession = applyOperation session operation
            
            // Recalculate affected orders using GenOrder.Lib
            let! recalculatedOrders = recalculateOrders updatedSession.WorkingOrders
            
            // Validate complete order set
            let! validationResults = validateOrderSet recalculatedOrders session.PatientContext
            
            // Update session with results
            let! finalSession = updateSessionWithResults updatedSession recalculatedOrders validationResults
        
        // Auto-sync to EHR if enabled
        let! syncResult = 
            if session.AutoSync then syncToEhr finalSession
            else async { return Ok() }
        
        return Ok {
            SessionId = sessionId
            UpdatedOrders = finalSession.WorkingOrders
            CalculatedScenarios = calculateAllScenarios finalSession
            ValidationResults = validationResults
            ClinicalAlerts = generateClinicalAlerts finalSession
            TreatmentPlanUpdate = calculateTreatmentPlan finalSession
            SyncStatus = syncResult
        }
}
```

## EHR Integration Contract

### 1. Session Lifecycle API
```fsharp
type IEhrIntegrationApi = {
    // Session Management
    InitializeSession: EhrSessionRequest -> Async<Result<SessionInitResponse, string[]>>
    CloseSession: SessionId -> Async<Result<SessionCloseResponse, string[]>>
    
    // Resource Management
    UpdateInstitutionalResources: InstitutionId -> ResourceUpdate -> Async<Result<unit, string[]>>
    GetResourceVersions: InstitutionId -> Async<Result<ResourceVersionInfo, string[]>>
    
    // Session Operations
    ProcessOrderOperations: SessionId -> OrderOperation[] -> Async<Result<SessionResponse, string[]>>
    SyncSessionToEhr: SessionId -> Async<Result<EhrSyncData, string[]>>
    GetSessionStatus: SessionId -> Async<Result<SessionStatus, string[]>>
}
```

### 2. Clinician Interface API
```fsharp
type IClinicianApi = {
    // Order CRUD (requires active session)
    CreateOrder: SessionId -> OrderRequest -> Async<Result<OrderCreated, string[]>>
    UpdateOrder: SessionId -> OrderId -> OrderUpdate -> Async<Result<OrderUpdated, string[]>>
    DeleteOrder: SessionId -> OrderId -> Async<Result<OrderDeleted, string[]>>
    SelectOrderScenario: SessionId -> OrderId -> ScenarioId -> Async<Result<ScenarioSelected, string[]>>
    
    // Session Queries
    GetOrderScenarios: SessionId -> OrderId -> Async<Result<OrderScenario[], string[]>>
    GetTreatmentPlan: SessionId -> Async<Result<TreatmentPlan, string[]>>
    ValidateAllOrders: SessionId -> Async<Result<ValidationResults, string[]>>
    
    // Session Control
    RequestEhrSync: SessionId -> Async<Result<SyncStatus, string[]>>
}
```

## Data Flow Architecture

### 1. Session Initialization Flow
```
1. EHR → GenPRES: InitializeSession(patient_data, existing_orders, resources)
2. GenPRES: Create temporary session, load resources, validate existing orders
3. GenPRES → EHR: SessionInitResponse(session_id, calculated_scenarios)
4. EHR → Clinician: Open GenPRES interface with session_id
```

### 2. Active Session Flow
```
1. Clinician → GenPRES: Order CRUD operations via session_id
2. GenPRES: Update session state, recalculate, validate
3. GenPRES → Clinician: Updated scenarios and validation results
4. Optional: GenPRES → EHR: Auto-sync if enabled
```

### 3. Session Finalization Flow
```
1. Clinician/EHR → GenPRES: FinalizeSession(session_id)
2. GenPRES: Prepare complete order changeset
3. GenPRES → EHR: FinalizedOrders(created, modified, deleted orders)
4. EHR: Persist changes to patient record
5. GenPRES: Discard all session data (stateless achieved)
```

## Implementation Strategy

### Phase 1: Session Abstraction Layer
Wrap existing architecture without breaking changes:
```fsharp
// Wrap current patient state in session container
type SessionContainer = {
    Session: EhrSession
    CurrentState: App.Elmish.State  // Existing state wrapped
    EhrSyncHandler: EhrSyncHandler
}

// Add session awareness to existing components
let SessionAwareApp sessionContainer =
    // Existing App.View with session context
    App.View {| 
        // Map existing props but mark patient as readonly
        patient = Some sessionContainer.Session.PatientContext
        // ... other existing props
    |}
```

### Phase 2: EHR Integration Endpoints
Add EHR APIs alongside existing functionality:
```fsharp
// Extend existing ServerApi
type IExtendedServerApi = {
    // Existing API preserved
    processCommand: Command -> Async<Result<Response, string[]>>
    testApi: unit -> Async<string>
    
    // New EHR integration API
    initializeEhrSession: EhrSessionRequest -> Async<Result<SessionInitResponse, string[]>>
    processSessionOperation: SessionId -> SessionOperation -> Async<Result<SessionResponse, string[]>>
    finalizeEhrSession: SessionId -> Async<Result<FinalizedOrders, string[]>>
}
```

### Phase 3: Complete Stateless Mode
Remove persistent patient storage, maintain only session-based operation:
```fsharp
// Remove from client state
type CleanClientState = {
    // REMOVED: Patient: Patient option
    // REMOVED: Persistent OrderContext
    
    ActiveSession: EhrSession option    // Temporary only
    WorkingOrders: Map<OrderId, Order>  // Session-scoped
    CachedResources: ResourceCache      // Institution data only
}
```

## Technical Analysis

### System Properties

**Responsibility Boundaries**
- **EHR**: Patient data, authentication, authorization, long-term storage, regulatory compliance
  - **Authentication**: Verifies user identity and maintains secure sessions
  - **Authorization**: Determines user permissions, role-based access controls, and patient access rights
  - **Session Context**: Provides complete authorization context to GenPRES so the service knows what the user can and cannot do
- **GenPRES**: Clinical calculations, validation, order optimization
  - **Authorization Enforcement**: Respects EHR-provided permissions when presenting options and validating operations
  - **No Identity Management**: Relies entirely on EHR for user identity and access decisions
- **Separation**: No overlap in data ownership or access control

**Deployment Properties**
- Horizontal scaling: Any GenPRES instance can handle any session
- No database management: No patient database backup, recovery, or maintenance
- Multi-tenant support: Single instance serves multiple EHR systems
- Reduced compliance burden: No PHI storage

**Integration Properties**
- EHR workflow integration: Each EHR integrates according to workflow requirements
- Version independence: EHR and GenPRES update independently
- Loose coupling: Patient data model changes don't require GenPRES updates

### Implementation Requirements

**Session Management**
- Session cleanup: Sessions must be reliably discarded to maintain stateless guarantee
- Memory management: Session data must be garbage collected properly
- Concurrent access: Multiple clinicians may work on same patient orders

**Performance Optimization**
- Session-scoped caching: Calculation results cached within session, discarded after
- Resource preloading: Institutional data must be efficiently cached for session startup
- Network efficiency: Balance comprehensive data transfer against multiple round trips

**Reliability Patterns**
- Session recovery: Handle service restarts during active sessions
- Timeout handling: Session expiration with EHR notification
- Error resilience: Failed operations must not corrupt session state

## Refactoring Strategy from Current Implementation

### 1. Wrap Existing State Management
```fsharp
// Current App.fs state becomes session-scoped
type SessionState = App.Elmish.State  // Existing state structure

type ServiceState = {
    ActiveSessions: Map<SessionId, SessionState>
    CachedResources: ResourceCache
    ServiceConfiguration: ServiceConfig
}
```

### 2. Add Session Lifecycle to Existing APIs
```fsharp
// Extend current ServerApi.fs
let processEhrCommand sessionId command = async {
    let! session = getActiveSession sessionId
    match session with
    | Some sessionState ->
        // Use existing Command.processCmd with session context
        let! result = Command.processCmd provider command
        let! updatedSession = updateSession sessionId result
        return Ok result
    | None -> return Error [| "Session not found" |]
}
```

### 3. Session-Aware Resource Management
```fsharp
// Keep existing resource caching but make it session-aware
type SessionResourceManager = {
    LoadForSession: SessionId -> InstitutionId -> Async<ResourceSet>
    GetCachedResources: InstitutionId -> ResourceVersions -> Async<ResourceSet>
    UpdateInstitutionalCache: InstitutionId -> ResourceUpdate -> Async<unit>
}
```

## Deployment and Operations

### 1. Container Configuration
```dockerfile
# Stateless service deployment
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=build /app/publish .

# Only resource cache storage needed
VOLUME ["/app/cache/resources"]

# No patient data volumes
# No database connections needed
# No backup/recovery for patient data

EXPOSE 8085
ENTRYPOINT ["dotnet", "GenPresService.dll"]
```

### 2. Environment Configuration
```bash
# Service configuration - no persistent patient storage
GENPRES_SERVICE_MODE=stateless_persistence
GENPRES_SESSION_TIMEOUT_MINUTES=30
GENPRES_MAX_CONCURRENT_SESSIONS=100
GENPRES_RESOURCE_CACHE_SIZE=2GB

# No patient database configuration needed
# No session storage configuration needed
# No backup configuration needed
```

### 3. Monitoring and Observability
```fsharp
type SessionMetrics = {
    ActiveSessionCount: int
    AverageSessionDuration: TimeSpan
    OrdersPerSession: float
    CalculationPerformance: PerformanceMetrics
    EhrSyncSuccess: float
    SessionCleanupRate: int
}
```

## EHR Integration Properties

### 1. Data Sovereignty
- EHR maintains full ownership of patient data
- GenPRES operations don't affect EHR compliance posture
- All prescription activities flow through EHR audit systems
- EHR handles all data backup and recovery

### 2. Workflow Integration
- GenPRES interface embedded in EHR workflow
- Session maintains clinical context during prescription process
- All clinical decisions transferred back to EHR for permanent storage
- EHR manages concurrent access to patient orders

### 3. Operational Properties
- GenPRES and EHR can update on different schedules
- Multiple EHR systems can share GenPRES instance
- GenPRES can be scaled independently based on calculation load

## Implementation Summary

### Stateless Architecture
- No patient data persistence: All patient information discarded after session closure
- No authentication storage: EHR handles all identity and access management
- Complete state transfer: All order modifications returned to EHR
- Session isolation: Each session independent with no cross-contamination

### Clinical Functionality
- Mathematical modeling during active sessions
- Prescription workflows with session context
- Cross-order validation within session scope
- Real-time optimization and feedback

### Integration Architecture
- EHR maintains complete control over data and workflows
- Shared computational resources without data coupling
- Clear boundaries for compliance responsibilities

GenPRES functions as a computational clinical service providing prescription support without data persistence requirements. The architecture enables multi-EHR deployment while maintaining clinical calculation capabilities.