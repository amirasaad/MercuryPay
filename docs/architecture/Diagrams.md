# MercuryPay - Complete Architecture Diagrams

---

## Diagram 1: High-Level System Context (C4 - Level 1)

```mermaid
C4Context
  title System Context diagram for MercuryPay

  Person(customer, "Customer", "End user making payments or applying for loans")
  Person(merchant, "Merchant", "Business accepting payments")
  Person(admin, "Admin", "System administrator")

  System(mercuryPay, "MercuryPay", "Intelligent Financial Orchestration Platform")

  System_Ext(bank, "Banking Partners", "External banks for settlement")
  System_Ext(creditBureau, "Credit Bureaus", "External risk data")
  System_Ext(emailService, "Email Service", "SendGrid/Twilio")
  
  Rel(customer, mercuryPay, "Makes payments, applies for loans")
  Rel(merchant, mercuryPay, "Views transactions, settlement reports")
  Rel(admin, mercuryPay, "Configures rules, monitors system")
  
  Rel(mercuryPay, bank, "Settles funds")
  Rel(mercuryPay, creditBureau, "Checks credit history")
  Rel(mercuryPay, emailService, "Sends notifications")
```

---

## Diagram 2: Container Diagram (C4 - Level 2)

```mermaid
C4Container
  title Container diagram for MercuryPay

  Person(customer, "Customer")
  Person(merchant, "Merchant")

  System_Boundary(mercuryPay, "MercuryPay") {
    Container(webApp, "Web Application", "React/TypeScript", "Customer and merchant portal")
    Container(mobileApp, "Mobile App", "React Native", "Mobile interface")
    Container(apiGateway, "API Gateway", "YARP/Envoy", "Routes requests, rate limiting, auth")
    
    Container(paymentService, "Payment Service", ".NET 8", "Payment processing")
    Container(walletService, "Wallet Service", ".NET 8", "Balance management")
    Container(lendingService, "Lending Service", ".NET 8", "Loan origination")
    Container(riskService, "Risk Service", "Python/.NET", "Fraud detection")
    Container(agentPlatform, "Agent Platform", "Python", "AI agents")
    Container(identityService, "Identity Service", ".NET 8", "Auth & API keys")
    
    ContainerDb(paymentDb, "Payment Database", "PostgreSQL", "Transactions")
    ContainerDb(walletDb, "Wallet Database", "PostgreSQL", "Balances, ledger")
    ContainerDb(lendingDb, "Lending Database", "PostgreSQL", "Loans, collateral")
    ContainerDb(riskDb, "Risk Database", "PostgreSQL", "Risk scores, rules")
    ContainerDb(agentDb, "Agent Database", "PostgreSQL", "Agent state, evaluations")
    ContainerDb(identityDb, "Identity Database", "PostgreSQL", "Users, keys")
    
    Container(messageBus, "Message Bus", "Kafka/Redis Streams", "Event propagation")
    Container(cache, "Cache", "Redis", "Session, rate limiting, idempotency")
    Container(aspireHost, ".NET Aspire Host", ".NET Aspire", "Orchestration, service discovery")
  }

  System_Ext(bank, "Bank APIs")
  System_Ext(creditBureau, "Credit Bureau APIs")

  Rel(customer, webApp, "Uses")
  Rel(customer, mobileApp, "Uses")
  Rel(merchant, webApp, "Uses")
  
  Rel(webApp, apiGateway, "API calls")
  Rel(mobileApp, apiGateway, "API calls")
  
  Rel(apiGateway, paymentService, "Routes", "HTTPS")
  Rel(apiGateway, walletService, "Routes", "HTTPS")
  Rel(apiGateway, lendingService, "Routes", "HTTPS")
  Rel(apiGateway, riskService, "Routes", "HTTPS")
  Rel(apiGateway, identityService, "Routes", "HTTPS")
  
  Rel(paymentService, paymentDb, "Read/Write")
  Rel(walletService, walletDb, "Read/Write")
  Rel(lendingService, lendingDb, "Read/Write")
  Rel(riskService, riskDb, "Read/Write")
  Rel(agentPlatform, agentDb, "Read/Write")
  Rel(identityService, identityDb, "Read/Write")
  
  Rel(paymentService, messageBus, "Publish/Subscribe")
  Rel(walletService, messageBus, "Publish/Subscribe")
  Rel(lendingService, messageBus, "Publish/Subscribe")
  Rel(riskService, messageBus, "Publish/Subscribe")
  Rel(agentPlatform, messageBus, "Subscribe/Publish")
  
  Rel(paymentService, cache, "Read/Write")
  Rel(walletService, cache, "Read/Write")
  
  Rel(paymentService, bank, "Calls")
  Rel(riskService, creditBureau, "Calls")
  
  UpdateLayoutConfig($c4ShapeInRow="3", $c4BoundaryInRow="1")
```

---

## Diagram 3: Deployment Architecture (Azure)

```mermaid
graph TB
    subgraph "Azure Cloud"
        subgraph "AKS Cluster"
            subgraph "Namespace: mercury-pay"
                PG[Payment Service Pod]
                WG[Wallet Service Pod]
                LG[Lending Service Pod]
                RG[Risk Service Pod]
                AG[Agent Platform Pod]
                IG[Identity Service Pod]
                GW[API Gateway Pod]
            end
            
            subgraph "Aspire Control Plane"
                AH[Aspire Host]
                SD[Service Discovery]
                OT[OpenTelemetry Collector]
            end
        end
        
        subgraph "Azure Managed Services"
            Redis[Azure Cache for Redis]
            
            subgraph "Azure Database for PostgreSQL"
                PDB[(Payment DB)]
                WDB[(Wallet DB)]
                LDB[(Lending DB)]
                RDB[(Risk DB)]
                ADB[(Agent DB)]
                IDB[(Identity DB)]
            end
            
            EH[Azure Event Hubs] 
            K8s[AKS]
            KV[Azure Key Vault]
            Monitor[Azure Monitor]
        end
        
        subgraph "Front Door"
            AFD[Azure Front Door]
            WAF[WAF Policy]
        end
        
        subgraph "CDN"
            CDN[Azure CDN]
        end
    end
    
    subgraph "External"
        Users[Users/Merchants]
        Banks[Banking Partners]
        Credit[Credit Bureaus]
    end
    
    Users --> AFD
    AFD --> WAF
    WAF --> GW
    GW --> PG & WG & LG & RG & AG & IG
    
    PG --> PDB
    WG --> WDB
    LG --> LDB
    RG --> RDB
    AG --> ADB
    IG --> IDB
    
    PG --> Redis
    WG --> Redis
    
    PG --> EH
    WG --> EH
    LG --> EH
    RG --> EH
    AG --> EH
    
    PG --> Banks
    RG --> Credit
    
    AH --> PG & WG & LG
    OT --> Monitor
    
    KV --> PG
    KV --> WG
    KV --> LG
```

---

## Diagram 4: Bounded Contexts & Event Flow

```mermaid
flowchart TB
    subgraph "Payment Context"
        direction TB
        P1[Payment Aggregate] --> PE1[PaymentInitiated]
        P1 --> PE2[PaymentAuthorized]
        P1 --> PE3[PaymentCaptured]
        P1 --> PE4[PaymentRefunded]
    end
    
    subgraph "Wallet Context"
        direction TB
        W1[Wallet Aggregate] --> WE1[WalletCredited]
        W1 --> WE2[WalletDebited]
        W1 --> WE3[HoldPlaced]
        W1 --> WE4[HoldReleased]
    end
    
    subgraph "Lending Context"
        direction TB
        L1[Loan Aggregate] --> LE1[LoanApplicationSubmitted]
        L1 --> LE2[LoanApproved]
        L1 --> LE3[LoanDisbursed]
        L1 --> LE4[RepaymentReceived]
    end
    
    subgraph "Risk Context"
        direction TB
        R1[RiskAssessment] --> RE1[PaymentRiskAssessed]
        R1 --> RE2[LoanRiskAssessed]
        R1 --> RE3[FraudDetected]
    end
    
    subgraph "Agent Context"
        direction TB
        A1[FraudAgent]
        A2[RetryAgent]
        A3[ReconciliationAgent]
        A4[CostOptimizationAgent]
    end
    
    subgraph "Event Bus"
        EB[(Kafka/Redis Streams)]
    end
    
    Payment Context --> EB
    Wallet Context --> EB
    Lending Context --> EB
    Risk Context --> EB
    
    EB --> Payment Context
    EB --> Wallet Context
    EB --> Lending Context
    EB --> Risk Context
    EB --> Agent Context
    
    Agent Context --> EB
```

---

## Diagram 5: Payment Flow Sequence (Event-Driven)

```mermaid
sequenceDiagram
    participant C as Customer
    participant G as API Gateway
    participant P as Payment Service
    participant R as Risk Service
    participant W as Wallet Service
    participant B as Event Bus
    participant A as Retry Agent
    
    C->>G: POST /payments
    G->>P: Create Payment
    
    P->>P: Validate, Create PaymentInitiated event
    P->>B: Publish PaymentInitiated
    
    B->>R: Consume PaymentInitiated
    R->>R: Risk Assessment
    R->>B: Publish PaymentRiskAssessed
    
    B->>P: Consume PaymentRiskAssessed
    
    alt Risk Approved
        P->>W: Request Hold (synchronous)
        W->>W: Place hold on wallet
        W->>B: Publish HoldPlaced
        W-->>P: Hold success
        
        P->>B: Publish PaymentAuthorized
        P-->>G: 200 OK (payment authorized)
        G-->>C: Payment authorized
        
        Note over P,W: Later: Capture
        C->>G: POST /payments/{id}/capture
        G->>P: Capture payment
        
        P->>W: Debit wallet (synchronous)
        W->>W: Debit funds
        W->>B: Publish WalletDebited
        
        P->>B: Publish PaymentCaptured
        
        B->>A: Consume PaymentCaptured
        A->>A: Log success metrics
    else Risk Blocked
        R->>B: Publish FraudDetected
        B->>P: Consume FraudDetected
        P->>P: Mark payment failed
        P-->>G: 400 Payment blocked
        G-->>C: Payment declined
    end
```

---

## Diagram 6: Loan Origination Saga (Process Manager Pattern)

```mermaid
stateDiagram-v2
    [*] --> ApplicationSubmitted: LoanApplicationSubmitted
    
    ApplicationSubmitted --> RiskAssessmentPending: Emit LoanApplicationSubmitted
    RiskAssessmentPending --> RiskAssessed: RiskService responds
    
    RiskAssessed --> CollateralLockPending: LoanApproved
    RiskAssessed --> Rejected: LoanRiskAssessed (reject)
    
    CollateralLockPending --> CollateralLocked: HoldPlaced (from Wallet)
    CollateralLockPending --> Compensating: HoldFailed
    
    CollateralLocked --> DisbursementPending: Emit CollateralLocked
    
    DisbursementPending --> Disbursed: WalletDebited (disbursement)
    DisbursementPending --> Compensating: DisbursementFailed
    
    Disbursed --> [*]: LoanDisbursed
    
    Compensating --> Rejected: Release collateral hold
    Rejected --> [*]: LoanRejected
    
    state Compensating {
        [*] --> ReleaseHold
        ReleaseHold --> EmitRejection
        EmitRejection --> [*]
    }
```

---

## Diagram 7: Multi-Agent System Architecture

```mermaid
graph TB
    subgraph "Event Stream"
        EB[Kafka Topics]
    end
    
    subgraph "Agent Platform"
        direction TB
        
        subgraph "Agent Coordinator"
            AC[Agent Orchestrator]
            RR[Rule Registry]
            SM[State Manager]
        end
        
        subgraph "Specialized Agents"
            FA[Fraud Detection Agent]
            RA[Retry Agent]
            RCA[Reconciliation Agent]
            COA[Cost Optimization Agent]
        end
        
        subgraph "Agent Evaluation Framework"
            ML[Metrics Logger]
            AD[Anomaly Detector]
            DB[Dashboard]
            FB[Feedback Loop]
        end
        
        subgraph "Agent Communication"
            AM[Agent Message Bus]
            PS[P2P Sharing]
        end
    end
    
    subgraph "External Systems"
        LLM[LLM Service]
        ML[ML Models]
    end
    
    EB --> FA
    EB --> RA
    EB --> RCA
    EB --> COA
    
    FA --> AM
    RA --> AM
    RCA --> AM
    COA --> AM
    
    AM --> AC
    AC --> RR
    AC --> SM
    
    FA --> ML
    FA --> LLM
    
    FA --> ML
    RA --> ML
    RCA --> ML
    COA --> ML
    
    ML --> AD
    AD --> DB
    DB --> FB
    FB --> FA
    FB --> RA
    
    FA --> EB
    RA --> EB
    RCA --> EB
    COA --> EB
```

---

## Diagram 8: Agent Collaboration - Retry with Fallback

```mermaid
sequenceDiagram
    participant P as Payment Service
    participant EB as Event Bus
    participant RA as Retry Agent
    participant FA as Fraud Agent
    participant W as Wallet Service
    participant R as Risk Service
    participant LLM as LLM Service
    
    P->>EB: PaymentCaptureFailed
    
    EB->>RA: Consume PaymentCaptureFailed
    
    RA->>RA: Check retry count
    
    alt Retry Count < 3
        RA->>EB: RetryPaymentCommand
        EB->>P: Retry capture
    else Max Retries Exceeded
        RA->>FA: Request fraud analysis
        FA->>R: Check risk history
        FA->>LLM: Analyze failure pattern
        LLM-->>FA: Pattern analysis
        
        FA->>FA: Decision logic
        alt Suspect Fraud
            FA->>EB: FraudDetected
            EB->>R: Process fraud alert
        else System Issue
            FA->>RA: Escalate to manual review
            RA->>EB: PaymentRequiresReview
        end
        
        RA->>W: Release hold
        W->>EB: HoldReleased
    end
```

---

## Diagram 9: CQRS + Event Sourcing Pattern (Payment Service)

```mermaid
graph TB
    subgraph "Command Side"
        CMD[Command Handler]
        AR[Payment Aggregate]
        ES[Event Store]
        
        CMD --> AR
        AR --> ES
    end
    
    subgraph "Event Bus"
        EB[Kafka]
    end
    
    subgraph "Query Side"
        subgraph "Projectors"
            P1[Payment Projector]
            P2[Transaction Projector]
            P3[Customer Projector]
        end
        
        subgraph "Read Databases"
            R1[(Payment Read DB - PostgreSQL)]
            R2[(Transaction Read DB - PostgreSQL)]
            R3[(Customer Read DB - MongoDB)]
        end
        
        subgraph "Cache"
            RC[Redis Cache]
        end
        
        subgraph "API"
            QAPI[Query API]
        end
    end
    
    ES --> EB
    
    EB --> P1
    EB --> P2
    EB --> P3
    
    P1 --> R1
    P2 --> R2
    P3 --> R3
    
    R1 --> RC
    R2 --> RC
    
    QAPI --> R1
    QAPI --> R2
    QAPI --> R3
    QAPI --> RC
```

---

## Diagram 10: .NET Aspire Orchestration

```mermaid
graph TB
    subgraph "Aspire AppHost"
        AH[AppHost Program.cs]
        
        subgraph "Service References"
            PS[Payment Service]
            WS[Wallet Service]
            LS[Lending Service]
            RS[Risk Service]
        end
        
        subgraph "Infrastructure Components"
            R[Redis Resource]
            P[PostgreSQL Resource]
            K[Kafka Resource]
        end
        
        subgraph "Service Discovery"
            SD[Service Discovery]
            EP[Endpoint Resolution]
        end
        
        subgraph "Observability"
            OT[OpenTelemetry]
            TR[Tracing]
            LG[Logging]
            MT[Metrics]
        end
        
        subgraph "Dashboard"
            DB[Aspire Dashboard]
        end
    end
    
    AH --> PS
    AH --> WS
    AH --> LS
    AH --> RS
    
    AH --> R
    AH --> P
    AH --> K
    
    PS --> SD
    WS --> SD
    LS --> SD
    RS --> SD
    
    PS --> OT
    WS --> OT
    LS --> OT
    RS --> OT
    
    OT --> TR
    OT --> LG
    OT --> MT
    
    TR --> DB
    LG --> DB
    MT --> DB
```

---

## Diagram 11: Database Replication & High Availability

```mermaid
graph TB
    subgraph "Azure Region - Primary"
        subgraph "Payment Service DB Cluster"
            PM[Primary - Payment DB]
            PS1[Replica - Payment DB Read-1]
            PS2[Replica - Payment DB Read-2]
            
            PM --> PS1
            PM --> PS2
        end
        
        subgraph "Wallet Service DB Cluster"
            WM[Primary - Wallet DB]
            WS1[Replica - Wallet DB Read-1]
            
            WM --> WS1
        end
    end
    
    subgraph "Azure Region - Secondary (DR)"
        subgraph "Payment Service DR"
            PM_DR[Primary DR - Payment DB]
            PS_DR1[Replica DR - Payment DB]
            
            PM -.-> PM_DR
        end
        
        subgraph "Wallet Service DR"
            WM_DR[Primary DR - Wallet DB]
        end
    end
    
    subgraph "Failover Manager"
        FM[Azure Traffic Manager]
        HL[Health Probes]
        AS[Auto Switch]
        
        HL --> PM
        HL --> WM
        HL --> PM_DR
        HL --> WM_DR
        
        AS --> FM
    end
    
    App[Application] --> FM
    FM --> PM
    FM --> WM
    FM -.-> PM_DR
    FM -.-> WM_DR
```

---

## Diagram 12: Security Architecture

```mermaid
graph TB
    subgraph "External"
        Client[Client Applications]
        ExtAPI[External API Consumers]
    end
    
    subgraph "Edge Security"
        WAF[Web Application Firewall]
        DDoS[DDoS Protection]
        IPFilter[IP Allowlisting]
    end
    
    subgraph "API Gateway Layer"
        RateLimit[Rate Limiting]
        Auth[Authentication]
        APIKey[API Key Validation]
        JWT[JWT Validation]
    end
    
    subgraph "Service Mesh"
        mTLS[mTLS between services]
        SPIFFE[Service Identity - SPIFFE]
    end
    
    subgraph "Secret Management"
        KV[Azure Key Vault]
        Rotation[Auto Key Rotation]
    end
    
    subgraph "Security Scanning"
        SAST[SAST - SonarQube]
        DAST[DAST - OWASP ZAP]
        SCA[SCA - Snyk]
        VulnScan[Container Scanning]
    end
    
    subgraph "Agent Security"
        AgentID[Agent Identity]
        AgentAuth[Agent Authentication]
        AgentScope[Agent Scoped Permissions]
    end
    
    subgraph "Monitoring & Audit"
        Audit[Audit Logging]
        SIEM[SIEM Integration]
        Alert[Security Alerts]
    end
    
    Client --> WAF
    ExtAPI --> WAF
    WAF --> DDoS
    DDoS --> IPFilter
    IPFilter --> RateLimit
    RateLimit --> Auth
    Auth --> APIKey
    Auth --> JWT
    APIKey --> mTLS
    JWT --> mTLS
    
    mTLS --> KV
    KV --> Rotation
    
    SAST --> Code
    DAST --> API
    SCA --> Dependencies
    
    AgentID --> AgentAuth
    AgentAuth --> AgentScope
    
    Audit --> SIEM
    SIEM --> Alert
```

---

## Diagram 13: CI/CD Pipeline

```mermaid
graph LR
    subgraph "Source"
        Git[GitHub Repository]
        PR[Pull Request]
    end
    
    subgraph "CI Pipeline"
        Build[Build]
        Unit[Unit Tests]
        SAST[SAST Scan]
        SCA[SCA Scan]
        Container[Container Build]
        Registry[Container Registry]
    end
    
    subgraph "CD Pipeline - Dev"
        DeployDev[Deploy to Dev]
        IntTests[Integration Tests]
        Contract[Contract Tests]
    end
    
    subgraph "CD Pipeline - Staging"
        DeployStg[Deploy to Staging]
        E2E[E2E Tests]
        Perf[Performance Tests]
        DAST[DAST Scan]
    end
    
    subgraph "CD Pipeline - Prod"
        Approval[Manual Approval]
        DeployProd[Deploy to Production]
        Smoke[Smoke Tests]
        Monitor[Monitoring]
    end
    
    Git --> PR
    PR --> Build
    Build --> Unit
    Unit --> SAST
    SAST --> SCA
    SCA --> Container
    Container --> Registry
    
    Registry --> DeployDev
    DeployDev --> IntTests
    IntTests --> Contract
    Contract --> DeployStg
    
    DeployStg --> E2E
    E2E --> Perf
    Perf --> DAST
    DAST --> Approval
    
    Approval --> DeployProd
    DeployProd --> Smoke
    Smoke --> Monitor
```

---

## Diagram 14: Observability Stack

```mermaid
graph TB
    subgraph "Services"
        S1[Payment Service]
        S2[Wallet Service]
        S3[Lending Service]
        S4[Agent Platform]
    end
    
    subgraph "Data Collection"
        OT[OpenTelemetry Collector]
        Prom[Prometheus]
        Loki[Loki - Logs]
        Tempo[Tempo - Traces]
    end
    
    subgraph "Storage"
        TSDB[Time Series DB]
        LogDB[Log Storage]
        TraceDB[Trace Storage]
    end
    
    subgraph "Visualization"
        Graf[Grafana]
        Dash[Business Dashboards]
        TechDash[Technical Dashboards]
    end
    
    subgraph "Alerting"
        Alert[Alert Manager]
        Pager[PagerDuty]
        Slack[Slack Alerts]
    end
    
    subgraph "Business Metrics"
        BM[Payment Volume]
        BM2[Loan Originations]
        BM3[Fraud Rate]
        BM4[Agent Success Rate]
    end
    
    S1 --> OT
    S2 --> OT
    S3 --> OT
    S4 --> OT
    
    S1 --> Prom
    S2 --> Prom
    S3 --> Prom
    S4 --> Prom
    
    OT --> Tempo
    OT --> Loki
    
    Prom --> TSDB
    Loki --> LogDB
    Tempo --> TraceDB
    
    TSDB --> Graf
    LogDB --> Graf
    TraceDB --> Graf
    
    Graf --> Dash
    Graf --> TechDash
    
    Graf --> Alert
    Alert --> Pager
    Alert --> Slack
    
    S1 --> BM
    S2 --> BM2
    S3 --> BM3
    S4 --> BM4
    
    BM --> Dash
    BM2 --> Dash
    BM3 --> Dash
    BM4 --> Dash
```

---
