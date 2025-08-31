# GenPRES Stateless Persistence Service Design Proposal

## Executive Summary

Transform GenPRES into a **stateless persistence service** that provides sophisticated clinical decision support to Electronic Patient Data (EPD) systems. The service maintains no permanent patient data or clinical state, operating through temporary sessions where all patient context is provided by the EPD and all modifications are transferred back upon session completion. GenPRES preserves its full clinical calculation capabilities while eliminating persistent data storage responsibilities.

## Core Design Principle: Stateless Persistence

### Definition
**Stateless Persistence** means:
- **Zero permanent patient storage**: No patient data, orders, or clinical decisions persisted beyond active sessions
- **Temporary working sessions**: Rich clinical context maintained during prescription workflows
- **Complete state transfer**: All session modifications returned to EPD system
- **Resource-only caching**: Only institutional rules, formularies, and product data cached for performance

### Service Boundary
```
Session Lifecycle:
EPD → [Start Session + Patient Data + Orders] → GenPRES Session
      ↓
Active Session: GenPRES ←→ Clinician Interface (CRUD Operations)
      ↓
EPD ← [Modified Orders + Clinical Decisions] ← Session End → [State Discarded]
```

GenPRES operates as a **computational clinical service** with temporary working memory but zero persistence responsibility.

## Architectural Design

### 1. EPD-Controlled Session Management

#### Session Initiation
```fsharp
type EpdSessionRequest = {
    SessionId: Guid
    EpdSystemId: string           // Identifying which EPD system
    EpdUserId: string            // EPD user reference (not stored)
    EpdPatientId: string         // EPD patient reference (not stored)
    PatientSnapshot: PatientData // Complete patient context for calculations
    ExistingOrders: EpdOrder[]   // Current patient orders from EPD
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
    EpdReference: EpdSystemId * EpdUserId * EpdPatientId  // References only
    PatientContext: PatientData      // Temporary clinical context
    WorkingOrders: Map<OrderId, Order>    // Active order workspace
    EpdOrderMapping: Map<EpdOrderId, GenPresOrderId>     // Order reference mapping
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
    | SyncToEpd                   // Manual sync during session
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
    ActiveSession: EpdSession option     // Temporary session context
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
    | EpdSessionStarted of EpdSessionRequest
    | EpdSessionClosed of SessionId
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
        // Display "Waiting for EPD session" state
        WaitingForEpdView()
    | Some session ->
        // Display patient context from session
        PatientView({
            patient = session.PatientContext
            readonly = true  // Patient data managed by EPD
            orders = session.WorkingOrders
            onOrderOperation = props.onOrderOperation
        })
```

### 3. Server Session Management

#### Session Store
```fsharp
type SessionStore = {
    ActiveSessions: Map<SessionId, GenPresSession>
    SessionsByEpd: Map<EpdSystemId, SessionId[]>
    ExpirationQueue: PriorityQueue<SessionId, DateTime>
}

let sessionManager = {
    CreateSession: EpdSessionRequest -> Async<Result<SessionResponse, string[]>>
    GetSession: SessionId -> Async<GenPresSession option>
    UpdateSession: SessionId -> SessionOperation -> Async<Result<SessionResponse, string[]>>
    SyncSession: SessionId -> Async<Result<EpdSyncData, string[]>>
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
        // Apply operation to session state
        let! updatedSession = applyOperation session operation
        
        // Recalculate affected orders using GenOrder.Lib
        let! recalculatedOrders = recalculateOrders updatedSession.WorkingOrders
        
        // Validate complete order set
        let! validationResults = validateOrderSet recalculatedOrders session.PatientContext
        
        // Update session with results
        let! finalSession = updateSessionWithResults updatedSession recalculatedOrders validationResults
        
        // Auto-sync to EPD if enabled
        let! syncResult = 
            if session.AutoSync then syncToEpd finalSession
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

## EPD Integration Contract

### 1. Session Lifecycle API
```fsharp
type IEpdIntegrationApi = {
    // Session Management
    InitializeSession: EpdSessionRequest -> Async<Result<SessionInitResponse, string[]>>
    CloseSession: SessionId -> Async<Result<SessionCloseResponse, string[]>>
    
    // Resource Management
    UpdateInstitutionalResources: InstitutionId -> ResourceUpdate -> Async<Result<unit, string[]>>
    GetResourceVersions: InstitutionId -> Async<Result<ResourceVersionInfo, string[]>>
    
    // Session Operations
    ProcessOrderOperations: SessionId -> OrderOperation[] -> Async<Result<SessionResponse, string[]>>
    SyncSessionToEpd: SessionId -> Async<Result<EpdSyncData, string[]>>
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
    RequestEpdSync: SessionId -> Async<Result<SyncStatus, string[]>>
}
```

## Data Flow Architecture

### 1. Session Initialization Flow
```
1. EPD → GenPRES: InitializeSession(patient_data, existing_orders, resources)
2. GenPRES: Create temporary session, load resources, validate existing orders
3. GenPRES → EPD: SessionInitResponse(session_id, calculated_scenarios)
4. EPD → Clinician: Open GenPRES interface with session_id
```

### 2. Active Session Flow
```
1. Clinician → GenPRES: Order CRUD operations via session_id
2. GenPRES: Update session state, recalculate, validate
3. GenPRES → Clinician: Updated scenarios and validation results
4. Optional: GenPRES → EPD: Auto-sync if enabled
```

### 3. Session Finalization Flow
```
1. Clinician/EPD → GenPRES: FinalizeSession(session_id)
2. GenPRES: Prepare complete order changeset
3. GenPRES → EPD: FinalizedOrders(created, modified, deleted orders)
4. EPD: Persist changes to patient record
5. GenPRES: Discard all session data (stateless achieved)
```

## Implementation Strategy

### Phase 1: Session Abstraction Layer
**Wrap existing architecture without breaking changes:**
```fsharp
// Wrap current patient state in session container
type SessionContainer = {
    Session: EpdSession
    CurrentState: App.Elmish.State  // Existing state wrapped
    EpdSyncHandler: EpdSyncHandler
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

### Phase 2: EPD Integration Endpoints
**Add EPD APIs alongside existing functionality:**
```fsharp
// Extend existing ServerApi
type IExtendedServerApi = {
    // Existing API preserved
    processCommand: Command -> Async<Result<Response, string[]>>
    testApi: unit -> Async<string>
    
    // New EPD integration API
    initializeEpdSession: EpdSessionRequest -> Async<Result<SessionInitResponse, string[]>>
    processSessionOperation: SessionId -> SessionOperation -> Async<Result<SessionResponse, string[]>>
    finalizeEpdSession: SessionId -> Async<Result<FinalizedOrders, string[]>>
}
```

### Phase 3: Complete Stateless Mode
**Remove persistent patient storage, maintain only session-based operation:**
```fsharp
// Remove from client state
type CleanClientState = {
    // REMOVED: Patient: Patient option
    // REMOVED: Persistent OrderContext
    
    ActiveSession: EpdSession option    // Temporary only
    WorkingOrders: Map<OrderId, Order>  // Session-scoped
    CachedResources: ResourceCache      // Institution data only
}
```

## Critical Assessment: Advantages of Stateless Persistence

### ✅ **Major Benefits**

**1. Clear Responsibility Boundaries**
- **EPD**: Patient data, authentication, long-term storage, regulatory compliance
- **GenPRES**: Sophisticated calculations, clinical validation, order optimization
- **Clean separation**: No overlap or confusion about data ownership

**2. Deployment and Scaling Advantages**
- **Horizontal scaling**: Any GenPRES instance can handle any session
- **No database management**: No patient database backup, recovery, or maintenance
- **Multi-tenant ready**: Single instance serves multiple EPD systems safely
- **Simplified compliance**: No PHI storage means reduced regulatory burden

**3. Enhanced Integration**
- **EPD system flexibility**: Each EPD can integrate according to its workflow needs
- **Version independence**: EPD and GenPRES can update independently
- **Reduced coupling**: Changes to patient data models don't require GenPRES updates

### ⚠️ **Implementation Considerations**

**1. Session Management Requirements**
- **Robust cleanup**: Must reliably discard expired sessions to maintain stateless guarantee
- **Memory management**: Session data must be properly garbage collected
- **Concurrent access**: Multiple clinicians potentially working on same patient orders

**2. Performance Optimization**
- **Session-scoped caching**: Calculation results cached within session but discarded after
- **Resource preloading**: Institutional data must be efficiently cached for session startup
- **Network efficiency**: Balance between comprehensive data transfer and multiple round trips

**3. Reliability Patterns**
- **Session recovery**: Handle service restarts during active sessions
- **Timeout handling**: Graceful session expiration with EPD notification
- **Error resilience**: Failed operations shouldn't corrupt session state

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
let processEpdCommand sessionId command = async {
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
    EpdSyncSuccess: float
    SessionCleanupRate: int
}
```

## Integration Benefits for EPD Systems

### 1. Data Sovereignty
- **Complete control**: EPD maintains full ownership of patient data
- **Compliance alignment**: GenPRES operations don't affect EPD compliance posture
- **Audit integration**: All prescription activities flow through EPD audit systems
- **Backup responsibility**: EPD handles all data backup and recovery

### 2. Workflow Integration
- **Embedded experience**: GenPRES interface embedded in EPD workflow
- **Context preservation**: Session maintains clinical context during prescription process
- **Decision capture**: All clinical decisions transferred back to EPD for permanent storage
- **Multi-user support**: EPD can manage concurrent access to patient orders

### 3. Operational Flexibility
- **Independent deployment**: GenPRES and EPD can update on different schedules
- **Shared resources**: Multiple EPD systems can share GenPRES instance efficiently
- **Performance scaling**: GenPRES can be scaled independently based on calculation load

## Conclusion: True Stateless Persistence

This design achieves **genuine stateless persistence** while preserving GenPRES's sophisticated clinical capabilities:

### ✅ **Stateless Achieved**
- **Zero patient data persistence**: All patient information discarded after session closure
- **No authentication storage**: EPD handles all identity and access management
- **Clean state transfer**: Complete order modifications returned to EPD
- **Session isolation**: Each session independent with no cross-contamination

### ✅ **Clinical Value Preserved**
- **Complex calculations**: Full mathematical modeling during active sessions
- **Treatment planning**: Comprehensive prescription workflows with session context
- **Drug interaction checking**: Cross-order validation within session scope
- **Real-time optimization**: Immediate feedback and scenario calculation

### ✅ **Integration Excellence**
- **EPD system autonomy**: Each EPD maintains complete control over its data and workflows
- **Service efficiency**: Shared computational resources without data coupling
- **Regulatory simplification**: Clear boundaries for compliance responsibilities

**GenPRES becomes a "computational clinical service"** - providing sophisticated prescription support without any data persistence burden. This architecture enables wide adoption across different EPD systems while maintaining the clinical sophistication that makes GenPRES valuable to healthcare providers.

The stateless persistence model represents an optimal balance between service-oriented architecture and clinical decision support functionality, enabling GenPRES to serve as a shared computational resource while preserving its core clinical value proposition.