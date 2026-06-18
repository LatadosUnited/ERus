# ADR 001: Sistema de Versionamento do Hub via GitHub Releases

**Data:** Junho de 2026  
**Status:** Aceito

## 1. Contexto e Problema
O `ERus.Hub` precisa ser capaz de baixar novas versões do `ERus.Editor/Engine` e instalá-las localmente na máquina do desenvolvedor (similar ao Unity Hub). O problema central era decidir **onde e como** hospedar esses arquivos compilados (zips de dezenas de megabytes) sem gerar custos altos com servidores dedicados (AWS/Azure) nem criar a necessidade de mantermos uma API backend customizada apenas para gerenciar downloads.

## 2. Decisão Arquitetural
Decidimos utilizar a **API pública do GitHub Releases** (`api.github.com/repos/{User}/{Repo}/releases`) para hospedar e distribuir as versões da engine. O Hub fará requisições HTTP REST diretamente contra essa API para listar as _tags_ disponíveis e processar os downloads dos _Assets_ (.zip) nativamente.

## 3. Consequências (Prós, Contras e Trade-offs)

### Positivas
- **Custo Zero:** Hospedagem de binários 100% gratuita via ecossistema do GitHub.
- **Zero-Backend:** Não há servidor próprio para ser monitorado ou dar _crash_. O Hub é um cliente gordo (_fat client_).
- **Integração CI/CD Fácil:** Podemos usar _GitHub Actions_ para rodar nosso script `build_engine_release.ps1` e anexar o zip diretamente na release de forma automatizada.

### Negativas / Limitações
- **Rate Limits do GitHub:** A API pública não-autenticada permite apenas 60 requisições por hora por IP. Se o Hub rodar o loop de _fetch_ de versões de forma muito agressiva, o limite será estourado (HTTP 403 Rate Limit Exceeded).
- **Sem Downloads Retomáveis Simples:** A implementação em `HttpClient` do Hub baixa o arquivo de uma vez. Pausar ou lidar com quedas de conexão requer uma refatoração manual mais complexa (_Byte Range Requests_).
- **Limites de Tamanho de Asset:** O GitHub recomenda que as _Releases_ não tenham anexos absurdamente grandes (máximo 2GB por arquivo). Atualmente o zip da engine pesa ~50MB, o que é seguro, mas o crescimento da engine deve ser monitorado.
