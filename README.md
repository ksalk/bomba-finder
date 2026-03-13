# Bomba Finder

A Discord bot that uses AI-powered semantic search to find quotes and dialogues from the Polish animated series "Kapitan Bomba" and related series. Simply type a quote or describe a scene, and the bot will locate the exact video and timestamp where it appears.

## How It Works

### Discord Bot Command Flow

When a user runs the `/bomba <search-text>` command in Discord:

1. **Command Handling**: The bot receives the slash command through Discord.Net and defers the response to handle potentially long search operations asynchronously.

2. **Query Processing**: The search text is normalized and an embedding vector is generated using OpenAI's text-embedding-3-large model via the OpenRouter API.

3. **Hybrid Search**: The bot performs two searches simultaneously in PostgreSQL:
   - **Trigram Similarity**: Uses PostgreSQL's `pg_trgm` extension for fuzzy text matching on transcript chunks
   - **Vector Similarity**: Uses the `pgvector` extension to compute cosine distance between query embedding and stored embeddings (3072 dimensions)

4. **Result Merging**: Results from both methods are combined, prioritizing matches that appear in both searches for higher accuracy.

5. **Response**: Returns a rich Discord embed containing:
   - Video title and thumbnail
   - Clickable link to the exact timestamp (YouTube format with `?t=Xs`)
   - Matching transcript snippet

### Transcript Fetching and Processing Pipeline

The `Bomba.TranscriptFetcher` console application handles content ingestion:

1. **Playlist Processing**: Iterates through YouTube playlists (Kapitan Bomba, Laserowy Gniew Dzidy, Galaktyczne Lektury)

2. **Transcript Extraction** (in order of preference):
   - Attempts to download Polish subtitles directly from YouTube
   - Falls back to audio transcription using Whisper.NET (local speech-to-text with `ggml-medium.bin` model)
   - Downloads audio via `yt-dlp` when transcription is needed

3. **Script Chunking**: Splits transcripts into overlapping segments at 30 and 50 character thresholds to improve search granularity and handle partial matches

4. **Embedding Generation**: Generates vector embeddings for each chunk using the OpenRouter API

5. **Storage**: Stores videos, transcripts, chunks, and embeddings in PostgreSQL with the pgvector extension

### AI Semantic Search

The semantic search system enables finding quotes by meaning rather than exact text:

- **Embedding Model**: OpenAI text-embedding-3-large produces 3072-dimensional vectors
- **Vector Database**: PostgreSQL with pgvector extension stores and indexes embeddings
- **Cosine Similarity**: Measures semantic similarity between query and transcript chunks
- **Normalization**: Text is lowercased and normalized before embedding generation

This allows users to search with paraphrases, approximate descriptions, or partial quotes and still find the correct video segment.

### Hybrid Search Strategy

The search combines two complementary approaches:

| Method | Strength | Use Case |
|--------|----------|----------|
| Trigram Similarity | Fast fuzzy text matching | Finding exact or near-exact quotes with typos |
| Vector Similarity | Semantic meaning matching | Finding quotes by description or paraphrase |

**Combining Results**:
- Chunks appearing in both result sets receive higher relevance scores
- Provides robust matching that handles both literal queries and conceptual searches
- Configurable similarity thresholds for balancing precision vs. recall
