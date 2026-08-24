from graphviz import Digraph

AZ  = "#0078D4"; AZL = "#E5F1FB"
AWS = "#FF9900"; AWSL= "#FFF4E5"
GY  = "#5A5A5A"; GYL = "#F0F0F0"
RD  = "#C42B1C"; RDL = "#FDE7E5"
GRN = "#107C10"; GRNL= "#E6F4EA"

def base(name, rankdir="TB"):
    g = Digraph(name, format="png")
    g.attr(rankdir=rankdir, bgcolor="white", fontname="Helvetica",
           splines="spline", nodesep="0.45", ranksep="0.55")
    g.attr("node", fontname="Helvetica", fontsize="11", shape="box",
           style="rounded,filled", penwidth="1.4", margin="0.16,0.10")
    g.attr("edge", fontname="Helvetica", fontsize="9", color=GY, penwidth="1.2")
    return g

# ---------- 1. Azure architecture (Sprint 4) ----------
g = base("azure_arch")
g.attr(label="Sprint 4 — Architecture Azure (rg-arkcloud-dev)", labelloc="t",
       fontsize="15", fontcolor=AZ)
g.node("net", "Internet", fillcolor=GYL, color=GY, shape="ellipse")

with g.subgraph(name="cluster_vnet") as c:
    c.attr(label="vnet-arkcloud-dev  (10.10.0.0/16)", style="rounded", color=AZ,
           fontcolor=AZ, fontname="Helvetica-Bold", fontsize="11", bgcolor="#FAFCFF")
    with c.subgraph(name="cluster_web") as s:
        s.attr(label="snet-web  +  nsg-web", style="rounded,dashed", color=AZ, fontsize="9")
        s.node("web", "App Service\napp-arkcloud-web-dev\nBlazor · Basic B1", fillcolor=AZL, color=AZ)
    with c.subgraph(name="cluster_api") as s:
        s.attr(label="snet-api  +  nsg-api", style="rounded,dashed", color=AZ, fontsize="9")
        s.node("api", "App Service\napp-arkcloud-api-dev\n.NET API · Basic B1", fillcolor=AZL, color=AZ)
    with c.subgraph(name="cluster_db") as s:
        s.attr(label="snet-database (délégué)", style="rounded,dashed", color=AZ, fontsize="9")
        s.node("pg", "PostgreSQL\nFlexible Server\nB_Standard_B1ms", fillcolor=AZL, color=AZ)
    with c.subgraph(name="cluster_pe") as s:
        s.attr(label="snet-private-endpoint", style="rounded,dashed", color="#B0B0B0", fontsize="9")
        s.node("pe", "réservé — vide\n(private endpoints, Sprint 6)",
               fillcolor="white", color="#B0B0B0", fontcolor="#808080", style="rounded,filled,dashed")

g.node("kv", "Key Vault\nkv-arkcloud-dev\nRBAC · purge protection", fillcolor=AZL, color=AZ)
g.node("ai", "Application Insights\n+ Log Analytics", fillcolor=AZL, color=AZ)

g.edge("net", "web", label="HTTPS 443")
g.edge("net", "api", label="HTTPS 443")
g.edge("web", "api", label="Api__BaseUrl")
g.edge("api", "pg", label="privé (VNet)")
g.edge("api", "kv", label="Managed Identity\nSecrets User", color=GRN, fontcolor=GRN)
g.edge("web", "ai", style="dashed", label="télémétrie")
g.edge("api", "ai", style="dashed")
g.edge("pg", "ai", style="dotted", label="diagnostics")
g.edge("kv", "ai", style="dotted")
g.attr(dpi="96"); g.render("img/01_azure_arch", cleanup=True)

# ---------- 2. AWS architecture (Sprint 5) ----------
g = base("aws_arch")
g.attr(label="Sprint 5 — Architecture AWS (eu-west-1)", labelloc="t",
       fontsize="15", fontcolor="#D97706")
g.node("net2", "Internet", fillcolor=GYL, color=GY, shape="ellipse")

with g.subgraph(name="cluster_vpc") as c:
    c.attr(label="vpc-arkcloud-dev", style="rounded", color=AWS, fontcolor="#D97706",
           fontname="Helvetica-Bold", fontsize="11", bgcolor="#FFFDF7")
    with c.subgraph(name="cluster_pub") as s:
        s.attr(label="subnets publics  ×2 AZ", style="rounded,dashed", color=AWS, fontsize="9")
        s.node("alb", "Application Load Balancer\nrouting par chemin", fillcolor=AWSL, color=AWS)
        s.node("nat", "NAT Gateway\n(jamais Free Tier)", fillcolor=AWSL, color=AWS)
    with c.subgraph(name="cluster_ecs") as s:
        s.attr(label="subnets ecs  ×2 AZ", style="rounded,dashed", color=AWS, fontsize="9")
        s.node("fapi", "Fargate — API\nsg-ecs-api", fillcolor=AWSL, color=AWS)
        s.node("fweb", "Fargate — Blazor\nsg-ecs-web", fillcolor=AWSL, color=AWS)
    with c.subgraph(name="cluster_dbs") as s:
        s.attr(label="subnets database  ×2 AZ — aucune route sortante", style="rounded,dashed",
               color=AWS, fontsize="9")
        s.node("rds", "RDS PostgreSQL\nsg-database", fillcolor=AWSL, color=AWS)

g.node("sm", "Secrets Manager\nconnexion · JWT", fillcolor=AWSL, color=AWS)
g.node("cw", "CloudWatch\nLogs · Alarmes · Dashboard", fillcolor=AWSL, color=AWS)
g.node("ct", "CloudTrail → S3\n+ CloudWatch Logs", fillcolor=AWSL, color=AWS)

g.edge("net2", "alb", label="HTTP 80 / HTTPS 443")
g.edge("alb", "fapi", label="/api/*")
g.edge("alb", "fweb", label="défaut")
g.edge("fapi", "rds", label="5432 — autorisé", color=GRN, fontcolor=GRN)
g.edge("fweb", "rds", label="JAMAIS autorisé", color=RD, fontcolor=RD, style="dashed")
g.edge("fapi", "sm", style="dashed")
g.edge("fapi", "nat", style="dotted")
g.edge("fweb", "nat", style="dotted")
g.edge("fapi", "cw", style="dotted")
g.edge("fweb", "cw", style="dotted")
g.attr(dpi="96"); g.render("img/02_aws_arch", cleanup=True)

# ---------- 3. CI/CD cross-repo ----------
g = base("cicd", rankdir="LR")
g.attr(label="Pipeline CI/CD — deux repos, deux clouds", labelloc="t",
       fontsize="15", fontcolor=GY)
with g.subgraph(name="cluster_app") as c:
    c.attr(label="repo ArkCloud (applicatif)", style="rounded", color=GY, fontcolor=GY,
           fontname="Helvetica-Bold", fontsize="10", bgcolor=GYL)
    c.node("push", "push sur develop", fillcolor="white", color=GY)
    c.node("build", "build + tests\n+ scan Trivy", fillcolor="white", color=GY)
    c.node("img", "image Docker", fillcolor="white", color=GY)
with g.subgraph(name="cluster_reg") as c:
    c.attr(label="registres — NON unifiés (dette assumée)", style="rounded,dashed",
           color=RD, fontcolor=RD, fontsize="10", bgcolor=RDL)
    c.node("ghcr", "GHCR\n→ Azure", fillcolor="white", color=RD)
    c.node("ecr", "ECR\n→ AWS", fillcolor="white", color=RD)
with g.subgraph(name="cluster_inf") as c:
    c.attr(label="repo ArkCloudInfra", style="rounded", color=AZ, fontcolor=AZ,
           fontname="Helvetica-Bold", fontsize="10", bgcolor=AZL)
    c.node("disp", "repository_dispatch", fillcolor="white", color=AZ)
    c.node("gate", "fmt · tflint · Checkov\nplan", fillcolor="white", color=AZ)
    c.node("apply", "apply\n(Environment gaté)", fillcolor="white", color=AZ)
g.node("azr", "Azure\nApp Services", fillcolor=AZL, color=AZ)
g.node("awsr", "AWS\nECS Fargate", fillcolor=AWSL, color=AWS)

g.edge("push", "build"); g.edge("build", "img")
g.edge("img", "ghcr"); g.edge("img", "ecr")
g.edge("img", "disp", label="OIDC", color=GRN, fontcolor=GRN)
g.edge("disp", "gate"); g.edge("gate", "apply")
g.edge("apply", "azr"); g.edge("apply", "awsr")
g.edge("ghcr", "azr", style="dashed"); g.edge("ecr", "awsr", style="dashed")
g.attr(dpi="96"); g.render("img/03_cicd", cleanup=True)

# ---------- 4. Multi-cloud : état réel vs cible ----------
g = base("multicloud", rankdir="LR")
g.attr(label="État réel à la clôture Sprint 5 — et cible déclarée", labelloc="t",
       fontsize="15", fontcolor=GY)
with g.subgraph(name="cluster_now") as c:
    c.attr(label="AUJOURD'HUI — deux déploiements parallèles, aucun lien",
           style="rounded", color=RD, fontcolor=RD, fontname="Helvetica-Bold",
           fontsize="10", bgcolor=RDL)
    c.node("az1", "Azure\nbase · JWT · registre", fillcolor=AZL, color=AZ)
    c.node("aw1", "AWS\nbase · JWT · registre", fillcolor=AWSL, color=AWS)
    c.node("gap", "aucune réplication\naucune bascule\nJWT incompatibles",
           fillcolor="white", color=RD, fontcolor=RD, shape="note", style="filled")
with g.subgraph(name="cluster_target") as c:
    c.attr(label="CIBLE — primaire + DR (warm standby)", style="rounded",
           color=GRN, fontcolor=GRN, fontname="Helvetica-Bold", fontsize="10", bgcolor=GRNL)
    c.node("dns", "couche DNS + health-check\nagnostique", fillcolor="white", color=GRN)
    c.node("prim", "Primaire\ntrafic 100 %", fillcolor="white", color=GRN)
    c.node("dr", "Secondaire\ncapacité réduite", fillcolor="white", color=GRN)
    c.node("repl", "réplication PostgreSQL\ncontinue", fillcolor="white", color=GRN)
g.edge("az1", "gap", style="dashed", color=RD, arrowhead="none")
g.edge("aw1", "gap", style="dashed", color=RD, arrowhead="none")
g.edge("dns", "prim"); g.edge("dns", "dr", style="dashed", label="bascule")
g.edge("prim", "repl"); g.edge("repl", "dr")
g.edge("gap", "dns", label="3 chantiers bloquants", color=GY, style="bold")
g.attr(dpi="96"); g.render("img/04_multicloud", cleanup=True)

# ---------- 5. Monitoring : les 3 bugs en cascade ----------
g = base("bugs", rankdir="LR")
g.attr(label="Sprint 4 — pourquoi le monitoring ne remontait rien", labelloc="t",
       fontsize="15", fontcolor=RD)
g.node("tf", "Terraform\n\"tout est câblé\"", fillcolor=AZL, color=AZ)
g.node("b1", "Bug 1\nSDK App Insights\njamais référencé", fillcolor=RDL, color=RD)
g.node("b2", "Bug 2\nGHCR privé\nimage jamais tirée", fillcolor=RDL, color=RD)
g.node("b3", "Bug 3\ntag \"latest\"\nn'a jamais existé", fillcolor=RDL, color=RD)
g.node("res", "0 ligne de télémétrie", fillcolor="white", color=RD, fontcolor=RD, shape="note", style="filled")
g.node("fix", "Vérifié par requête réelle\naprès correction", fillcolor=GRNL, color=GRN, fontcolor=GRN)
g.edge("tf", "b1"); g.edge("b1", "b2"); g.edge("b2", "b3"); g.edge("b3", "res")
g.edge("res", "fix", color=GRN, style="bold", label="3 correctifs")
g.attr(dpi="96"); g.render("img/05_bugs", cleanup=True)

print("OK")
