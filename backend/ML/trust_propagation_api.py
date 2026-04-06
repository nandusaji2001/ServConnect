"""
Trust Propagation API
FastAPI service for propagating trust score penalties when users are banned
"""

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field
from typing import List, Dict, Optional
from trust_propagation_service import TrustPropagationService
import uvicorn

app = FastAPI(
    title="Trust Propagation API",
    description="Propagates trust score penalties through social graph when users are banned",
    version="1.0.0"
)

# Global service instance
trust_service = TrustPropagationService()

# Request/Response Models
class FollowerRelationship(BaseModel):
    follower_id: str = Field(..., description="ID of the follower")
    following_id: str = Field(..., description="ID of the user being followed")

class UserInteraction(BaseModel):
    user_id: str = Field(..., description="ID of the user")
    target_user_id: Optional[str] = Field(None, description="ID of the target user")
    type: str = Field(..., description="Type of interaction (like, comment, share)")
    weight: float = Field(0.5, ge=0.0, le=1.0, description="Interaction weight")

class TrustScore(BaseModel):
    user_id: str
    trust_score: float = Field(..., ge=0.0, le=1.0)

class BuildGraphRequest(BaseModel):
    followers: List[FollowerRelationship] = Field(..., description="List of follower relationships")
    interactions: List[UserInteraction] = Field(default=[], description="List of user interactions")
    trust_scores: Dict[str, float] = Field(..., description="Current trust scores for users")

class PropagateBanRequest(BaseModel):
    banned_user_id: str = Field(..., description="ID of the banned user")
    max_hops: int = Field(2, ge=1, le=3, description="Maximum propagation distance")
    base_penalty: float = Field(0.15, ge=0.0, le=0.5, description="Base penalty for direct connections")

class AffectedUserInfo(BaseModel):
    new_user_trust_score: float
    new_content_trust_score: float
    penalty_applied: float
    distance: int
    relationship_type: str
    connection_weight: float

class PropagateBanResponse(BaseModel):
    success: bool
    affected_users: Dict[str, AffectedUserInfo]
    summary: Dict

class RecoveryRequest(BaseModel):
    user_id: str
    days_since_penalty: int = Field(..., ge=0)
    recovery_rate: float = Field(0.01, ge=0.0, le=0.1)

class RecoveryResponse(BaseModel):
    user_id: str
    new_user_trust_score: float
    new_content_trust_score: float
    recovery_applied: float


@app.get("/")
def root():
    """API health check"""
    return {
        "service": "Trust Propagation API",
        "status": "running",
        "version": "1.0.0"
    }

@app.post("/build-graph")
def build_graph(request: BuildGraphRequest):
    """
    Build social graph from follower relationships and interactions
    """
    try:
        # Convert to dict format
        followers = [f.dict() for f in request.followers]
        interactions = [i.dict() for i in request.interactions]
        
        # Build graph
        trust_service.build_social_graph(followers, interactions)
        trust_service.set_user_trust_scores(request.trust_scores)
        
        return {
            "success": True,
            "message": "Social graph built successfully",
            "stats": {
                "num_users": len(trust_service.user_graph),
                "num_connections": sum(len(v) for v in trust_service.user_graph.values()) // 2,
                "num_followers": len(followers),
                "num_interactions": len(interactions)
            }
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error building graph: {str(e)}")

@app.post("/propagate-ban", response_model=PropagateBanResponse)
def propagate_ban(request: PropagateBanRequest):
    """
    Propagate trust score penalties when a user is banned
    
    Returns affected users with updated trust scores
    """
    try:
        if not trust_service.user_graph:
            raise HTTPException(
                status_code=400, 
                detail="Social graph not built. Call /build-graph first."
            )
        
        # Propagate penalties
        affected_users = trust_service.propagate_ban_penalty(
            banned_user_id=request.banned_user_id,
            max_hops=request.max_hops,
            base_penalty=request.base_penalty
        )
        
        # Get summary
        summary = trust_service.get_affected_users_summary(affected_users)
        
        return PropagateBanResponse(
            success=True,
            affected_users=affected_users,
            summary=summary
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error propagating ban: {str(e)}")

@app.post("/compute-recovery", response_model=RecoveryResponse)
def compute_recovery(request: RecoveryRequest):
    """
    Compute trust score recovery for a user over time
    """
    try:
        recovery_data = trust_service.compute_trust_recovery(
            user_id=request.user_id,
            days_since_penalty=request.days_since_penalty,
            recovery_rate=request.recovery_rate
        )
        
        return RecoveryResponse(
            user_id=request.user_id,
            **recovery_data
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error computing recovery: {str(e)}")

@app.get("/graph-stats")
def get_graph_stats():
    """Get statistics about the current social graph"""
    if not trust_service.user_graph:
        return {
            "graph_built": False,
            "message": "No graph built yet"
        }
    
    num_users = len(trust_service.user_graph)
    num_connections = sum(len(v) for v in trust_service.user_graph.values()) // 2
    
    avg_connections = num_connections / num_users if num_users > 0 else 0
    
    return {
        "graph_built": True,
        "num_users": num_users,
        "num_connections": num_connections,
        "avg_connections_per_user": round(avg_connections, 2),
        "num_trust_scores": len(trust_service.user_trust_scores)
    }

@app.post("/reset")
def reset_graph():
    """Reset the social graph (for testing)"""
    global trust_service
    trust_service = TrustPropagationService()
    return {
        "success": True,
        "message": "Social graph reset successfully"
    }


if __name__ == "__main__":
    print("Starting Trust Propagation API...")
    print("API will be available at: http://localhost:8006")
    print("Documentation at: http://localhost:8006/docs")
    uvicorn.run(app, host="0.0.0.0", port=8006)
