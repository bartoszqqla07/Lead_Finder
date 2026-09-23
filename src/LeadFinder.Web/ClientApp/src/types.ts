// Kontrakty API – odpowiedniki rekordów z Contracts/Dtos.cs (enumy jako tekst).

export type LeadStatus = 'NoWebsite' | 'WebsiteDown' | 'WordPress' | 'HasWebsite';
export type OutreachStage = 'New' | 'Contacted' | 'Replied' | 'Client' | 'Rejected';
export type SearchRunState = 'Running' | 'Completed' | 'Failed' | 'Cancelled';
export type SearchStage = 'Searching' | 'CheckingWebsites';
export type ApiKeySource = 'None' | 'Environment' | 'App';

export interface Lead {
  id: number;
  placeId: string;
  name: string;
  address: string | null;
  phone: string | null;
  websiteUri: string | null;
  rating: number | null;
  userRatingCount: number | null;
  city: string;
  categoryId: string;
  categoryName: string;
  status: LeadStatus;
  statusLabel: string;
  priority: number;
  checkNote: string | null;
  technology: string | null;
  profilePlatform: string | null;
  stage: OutreachStage;
  notes: string;
  stageChangedAt: string | null;
  consentGivenAt: string | null;
  firstSeenAt: string;
  lastSeenAt: string;
  firstSearchRunId: number;
  lastSearchRunId: number;
  drafts: MessageDraft[];
  googleMapsUrl: string;
}

export type DraftKind = 'ProblemNotice' | 'DirectMessage' | 'Email' | 'Letter' | 'Proposal' | 'FollowUp';

export interface MessageDraft {
  kind: DraftKind;
  title: string;
  channel: string;
  guidance: string;
  subject: string | null;
  body: string;
  requiresConsent: boolean;
}

export interface LeadChanges {
  stage?: OutreachStage;
  notes?: string;
  consentGiven?: boolean;
}

export interface SearchRun {
  id: number;
  city: string;
  categories: string[];
  pages: number;
  state: SearchRunState;
  startedAt: string;
  finishedAt: string | null;
  foundCount: number;
  newCount: number;
  error: string | null;
}

export interface CurrentSearch {
  run: SearchRun;
  isRunning: boolean;
}

export type SearchEventType = 'progress' | 'completed' | 'failed' | 'cancelled';

export interface SearchEvent {
  type: SearchEventType;
  stage: SearchStage | null;
  message: string;
  current: number;
  total: number;
  at: string;
  run: SearchRun | null;
}

export interface Category {
  id: string;
  query: string;
  englishName: string;
}

export interface Settings {
  hasApiKey: boolean;
  apiKeySource: ApiKeySource;
  apiKeyHint: string | null;
  senderName: string;
  signature: string;
  contactEmail: string;
  postalAddress: string;
  defaultSenderName: string;
  defaultSignature: string;
  dataDirectory: string;
}

export interface UpdateSettings {
  apiKey?: string | null;
  senderName?: string;
  signature?: string;
  contactEmail?: string;
  postalAddress?: string;
}

export interface StartSearch {
  city: string;
  categoryIds: string[];
  pages: number;
}
